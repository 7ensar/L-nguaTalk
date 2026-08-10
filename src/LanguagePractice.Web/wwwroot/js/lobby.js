(() => {
  const cfg = window.LINGUATALK || {};
  const t = cfg.i18n || {};
  const toast = window.LinguaToast || {
    info: console.log,
    success: console.log,
    warn: console.warn,
    error: console.error
  };

  const statusText = document.getElementById("statusText");
  const joinBtn = document.getElementById("joinQueueBtn");
  const leaveBtn = document.getElementById("leaveQueueBtn");
  const nextBtn = document.getElementById("nextBtn");
  const reportBtn = document.getElementById("reportBtn");
  const reportFab = document.getElementById("reportFab");
  const backToLobbyBtn = document.getElementById("backToLobbyBtn");
  const reportForm = document.getElementById("reportForm");
  const reportModalEl = document.getElementById("reportModal");
  const mediaPermissionModalEl = document.getElementById("mediaPermissionModal");
  const mediaPermissionEnableBtn = document.getElementById("mediaPermissionEnableBtn");
  const mediaPermissionCancelBtn = document.getElementById("mediaPermissionCancelBtn");
  const mediaPermissionHint = document.getElementById("mediaPermissionHint");
  const localVideo = document.getElementById("localVideo");
  const remoteVideo = document.getElementById("remoteVideo");
  const videoEmpty = document.getElementById("videoEmpty");
  const chatMessages = document.getElementById("chatMessages");
  const chatForm = document.getElementById("chatForm");
  const chatInput = document.getElementById("chatInput");
  const chatSendBtn = document.getElementById("chatSendBtn");
  const chatPeerLabel = document.getElementById("chatPeerLabel");
  const toggleMicBtn = document.getElementById("toggleMicBtn");
  const toggleCamBtn = document.getElementById("toggleCamBtn");
  const switchCamBtn = document.getElementById("switchCamBtn");
  const topicText = document.getElementById("topicText");
  const nextTopicBtn = document.getElementById("nextTopicBtn");
  const queueHintText = document.getElementById("queueHintText");
  const socialActions = document.getElementById("socialActions");
  const friendBtn = document.getElementById("friendBtn");
  const blockBtn = document.getElementById("blockBtn");
  const ratingModalEl = document.getElementById("ratingModal");

  let socket = null;
  let pc = null;
  let localStream = null;
  let currentRoomId = null;
  let peerName = null;
  let peerSocketId = null;
  let peerUserId = null;
  let peerGuestSessionId = null;
  let inQueue = false;
  let isOfferer = false;
  let busy = false;
  let suppressAutoRequeue = false;
  let mediaGateOpen = false;
  let resumeJoinAfterMedia = false;
  let micEnabled = true;
  let camEnabled = true;
  /** @type {"user"|"environment"} */
  let preferredFacingMode = "user";
  let switchingCamera = false;
  let matchStartedAt = null;
  let topicIndex = 0;
  const topics = Array.isArray(cfg.topics) ? cfg.topics : [];
  const ratingModal = ratingModalEl && window.bootstrap
    ? bootstrap.Modal.getOrCreateInstance(ratingModalEl)
    : null;
  /** @type {RTCIceCandidateInit[]} */
  let pendingRemoteIce = [];
  const reportModal = reportModalEl && window.bootstrap
    ? bootstrap.Modal.getOrCreateInstance(reportModalEl)
    : null;
  const mediaPermissionModal = mediaPermissionModalEl && window.bootstrap
    ? bootstrap.Modal.getOrCreateInstance(mediaPermissionModalEl, {
        backdrop: "static",
        keyboard: false
      })
    : null;

  function getSelectedLanguage() {
    return (cfg.languageCode || "en").toLowerCase();
  }

  function format(template, ...args) {
    return String(template || "").replace(/\{(\d+)\}/g, (_, i) => args[i] ?? "");
  }

  function setStatus(message) {
    if (statusText) statusText.textContent = message;
  }

  function setRemoteVisible(hasRemote) {
    if (videoEmpty) videoEmpty.style.display = hasRemote ? "none" : "grid";
  }

  function escapeHtml(value) {
    return String(value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function clearChat() {
    if (!chatMessages) return;
    chatMessages.innerHTML = `<div class="chat-empty">${escapeHtml(t.chatEmpty || "Messages appear here when you match.")}</div>`;
  }

  function setChatEnabled(enabled) {
    if (chatInput) chatInput.disabled = !enabled;
    if (chatSendBtn) chatSendBtn.disabled = !enabled;
    if (chatPeerLabel) {
      chatPeerLabel.textContent = enabled && peerName
        ? format(t.chatWith || "Chatting with {0}", peerName)
        : (t.chatIdle || "Chat unlocks after match");
    }
  }

  const MAX_CHAT_BUBBLES = 80;
  const MAX_PENDING_ICE = 40;

  function appendChatMessage({ text, fromName, fromSocketId, mine }) {
    if (!chatMessages) return;
    const empty = chatMessages.querySelector(".chat-empty");
    if (empty) empty.remove();

    const row = document.createElement("div");
    row.className = `chat-bubble ${mine ? "mine" : "theirs"}`;
    row.innerHTML = `
      <div class="chat-bubble-meta">${escapeHtml(mine ? (cfg.displayName || "You") : (fromName || "Peer"))}</div>
      <div class="chat-bubble-text">${escapeHtml(text)}</div>
    `;
    chatMessages.appendChild(row);

    const bubbles = chatMessages.querySelectorAll(".chat-bubble");
    if (bubbles.length > MAX_CHAT_BUBBLES) {
      const removeCount = bubbles.length - MAX_CHAT_BUBBLES;
      for (let i = 0; i < removeCount; i += 1) {
        bubbles[i].remove();
      }
    }

    chatMessages.scrollTop = chatMessages.scrollHeight;
  }

  function setControls({ queued, matched }) {
    const blocked = mediaGateOpen || busy;
    const canReport = !!matched && !mediaGateOpen;
    if (joinBtn) {
      joinBtn.disabled = !!queued || !!matched || blocked;
      joinBtn.hidden = !!matched || !!queued;
    }
    if (leaveBtn) leaveBtn.disabled = !(queued || matched) || mediaGateOpen;
    if (nextBtn) nextBtn.disabled = blocked;
    if (reportBtn) reportBtn.disabled = !canReport;
    if (reportFab) reportFab.hidden = !canReport;
    if (backToLobbyBtn) backToLobbyBtn.hidden = !(queued || matched);
    if (socialActions) socialActions.hidden = !matched || !!cfg.isGuest;
    setChatEnabled(!!matched && !mediaGateOpen);
  }

  function showTopic(index) {
    if (!topicText || !topics.length) return;
    topicIndex = ((index % topics.length) + topics.length) % topics.length;
    topicText.textContent = topics[topicIndex];
  }

  function notifyMatch(title, body) {
    if (!cfg.browserNotifications || typeof Notification === "undefined") return;
    if (Notification.permission === "granted") {
      try { new Notification(title, { body, silent: false }); } catch (_) { /* ignore */ }
    } else if (Notification.permission === "default") {
      Notification.requestPermission().catch(() => {});
    }
  }

  function buildIceServers() {
    const fromCfg = Array.isArray(cfg.iceServers) ? cfg.iceServers : [];
    const mapped = fromCfg
      .map((s) => {
        const urls = s.urls || s.Urls;
        if (!urls || (Array.isArray(urls) && urls.length === 0)) return null;
        const row = { urls };
        if (s.username || s.Username) row.username = s.username || s.Username;
        if (s.credential || s.Credential) row.credential = s.credential || s.Credential;
        return row;
      })
      .filter(Boolean);
    if (mapped.length) return mapped;
    return [
      { urls: "stun:stun.l.google.com:19302" },
      { urls: "stun:stun1.l.google.com:19302" }
    ];
  }

  function queuePayload() {
    return {
      displayName: cfg.displayName || "Guest",
      languageCode: getSelectedLanguage(),
      userId: cfg.userId || null,
      guestSessionId: cfg.guestSessionId || null,
      languageLevel: cfg.languageLevel ?? null,
      gender: cfg.gender ?? null,
      preferredPartnerGender: cfg.preferredPartnerGender ?? null,
      interests: cfg.interests || [],
      preferSimilarLevel: cfg.preferSimilarLevel !== false,
      preferSharedInterests: cfg.preferSharedInterests !== false,
      isPremium: !!cfg.isPremium,
      blockedUserIds: cfg.blockedUserIds || [],
      rematchWithUserId: cfg.rematchWithUserId || null
    };
  }

  async function completeMatchOnServer() {
    if (!currentRoomId || !matchStartedAt) return;
    const durationSeconds = Math.max(0, Math.round((Date.now() - matchStartedAt) / 1000));
    const roomId = currentRoomId;
    try {
      await fetch(`/api/social/matches/${encodeURIComponent(roomId)}/complete`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({ durationSeconds })
      });
    } catch (_) { /* ignore */ }
  }

  function openRatingIfNeeded() {
    if (!cfg.userId || !currentRoomId || cfg.isGuest) return;
    if (ratingModalEl) ratingModalEl.dataset.roomId = currentRoomId;
    ratingModal?.show();
  }

  function syncMediaToggleUi() {
    if (toggleMicBtn) {
      toggleMicBtn.classList.toggle("is-off", !micEnabled);
      toggleMicBtn.setAttribute("aria-pressed", micEnabled ? "true" : "false");
      toggleMicBtn.textContent = micEnabled ? "Mic" : "Mic off";
    }
    if (toggleCamBtn) {
      toggleCamBtn.classList.toggle("is-off", !camEnabled);
      toggleCamBtn.setAttribute("aria-pressed", camEnabled ? "true" : "false");
      toggleCamBtn.textContent = camEnabled ? "Cam" : "Cam off";
    }
    if (switchCamBtn) {
      switchCamBtn.disabled = switchingCamera;
      switchCamBtn.setAttribute(
        "aria-label",
        t.switchCamera || "Switch camera"
      );
      switchCamBtn.title = t.switchCamera || "Switch camera";
    }
  }

  function syncLocalVideoMirror() {
    if (!localVideo) return;
    localVideo.classList.toggle("is-rear", preferredFacingMode === "environment");
  }

  function buildVideoConstraints(facing, deviceId) {
    const base = {
      width: { ideal: 640 },
      height: { ideal: 480 },
      frameRate: { ideal: 24, max: 30 }
    };
    if (deviceId) {
      return { ...base, deviceId: { exact: deviceId } };
    }
    return { ...base, facingMode: { ideal: facing || "user" } };
  }

  async function resolveFacingDeviceId(facing) {
    if (!navigator.mediaDevices?.enumerateDevices) return null;
    const devices = await navigator.mediaDevices.enumerateDevices();
    const videos = devices.filter((d) => d.kind === "videoinput");
    if (videos.length < 2) return null;

    const isBack = (label) => /back|rear|environment|arka|world/i.test(label || "");
    const isFront = (label) => /front|user|face|ön|facing you/i.test(label || "");

    if (facing === "environment") {
      const back = videos.find((d) => isBack(d.label));
      if (back?.deviceId) return back.deviceId;
    } else {
      const front = videos.find((d) => isFront(d.label));
      if (front?.deviceId) return front.deviceId;
    }

    const currentId = localStream?.getVideoTracks()[0]?.getSettings?.()?.deviceId;
    const idx = Math.max(0, videos.findIndex((d) => d.deviceId === currentId));
    const next = videos[(idx + 1) % videos.length];
    return next?.deviceId || null;
  }

  async function openVideoTrack(facing) {
    const currentId = localStream?.getVideoTracks()[0]?.getSettings?.()?.deviceId || null;

    async function fromConstraints(video) {
      const stream = await navigator.mediaDevices.getUserMedia({ video, audio: false });
      const track = stream.getVideoTracks()[0];
      if (!track) {
        stream.getTracks().forEach((tr) => tr.stop());
        throw new Error("NO_VIDEO_TRACK");
      }
      return { stream, track };
    }

    const deviceId = await resolveFacingDeviceId(facing);
    if (deviceId && deviceId !== currentId) {
      try {
        return await fromConstraints(buildVideoConstraints(facing, deviceId));
      } catch (_) {
        /* fall through to facingMode */
      }
    }

    try {
      const result = await fromConstraints({
        ...buildVideoConstraints(facing),
        facingMode: { exact: facing }
      });
      return result;
    } catch (_) {
      /* soft ideal constraint */
    }

    const soft = await fromConstraints(buildVideoConstraints(facing));
    const gotId = soft.track.getSettings?.()?.deviceId;
    if (currentId && gotId && gotId === currentId) {
      let videoCount = 0;
      try {
        const devices = await navigator.mediaDevices.enumerateDevices();
        videoCount = devices.filter((d) => d.kind === "videoinput").length;
      } catch (_) {
        videoCount = 0;
      }
      if (videoCount < 2) {
        soft.track.stop();
        throw new Error("NO_ALT_CAMERA");
      }
      const gotFacing = soft.track.getSettings?.()?.facingMode;
      if (!gotFacing || gotFacing !== facing) {
        soft.track.stop();
        throw new Error("SAME_CAMERA");
      }
    }
    return soft;
  }

  async function applyVideoTrack(newTrack) {
    newTrack.enabled = camEnabled;

    if (pc) {
      const sender = pc.getSenders().find((s) => s.track?.kind === "video");
      if (sender) {
        await sender.replaceTrack(newTrack);
      }
    }

    if (localStream) {
      const oldVideo = localStream.getVideoTracks()[0];
      if (oldVideo) {
        localStream.removeTrack(oldVideo);
        try {
          oldVideo.onended = null;
          oldVideo.stop();
        } catch (_) {
          /* ignore */
        }
      }
      localStream.addTrack(newTrack);
    } else {
      localStream = new MediaStream([newTrack]);
    }

    if (localVideo) {
      localVideo.srcObject = localStream;
      localVideo.muted = true;
      localVideo.playsInline = true;
      try {
        await localVideo.play();
      } catch (_) {
        /* autoplay policies */
      }
    }

    watchLocalTracks();
  }

  async function switchCamera() {
    if (switchingCamera || mediaGateOpen) return;
    if (!navigator.mediaDevices?.getUserMedia) {
      toast.warn(t.toastSwitchCameraFailed || "Could not switch camera on this device.");
      return;
    }

    const nextFacing = preferredFacingMode === "user" ? "environment" : "user";
    switchingCamera = true;
    syncMediaToggleUi();

    try {
      if (!hasLiveMedia()) {
        const previousFacing = preferredFacingMode;
        preferredFacingMode = nextFacing;
        try {
          await ensureMedia({ force: true });
        } catch (err) {
          preferredFacingMode = previousFacing;
          throw err;
        }
        return;
      }

      const { stream, track } = await openVideoTrack(nextFacing);
      await applyVideoTrack(track);
      // Drop empty leftover MediaStream reference; track now lives on localStream.
      stream.getTracks().forEach((tr) => {
        if (tr !== track) {
          try { tr.stop(); } catch (_) { /* ignore */ }
        }
      });
      preferredFacingMode = nextFacing;
      syncLocalVideoMirror();
    } catch (err) {
      console.warn("Camera switch failed", err);
      toast.warn(t.toastSwitchCameraFailed || "Could not switch camera on this device.");
    } finally {
      switchingCamera = false;
      syncMediaToggleUi();
    }
  }

  function openReportModal() {
    if (mediaGateOpen) return;
    if (!currentRoomId) {
      toast.warn(t.toastReportNoPeer || "No active peer to report.");
      return;
    }
    reportModal?.show();
  }

  function hasLiveMedia() {
    return !!localStream
      && localStream.getTracks().some((track) => track.readyState === "live");
  }

  function isMediaPermissionError(err) {
    const name = String(err?.name || "");
    if (name === "NotAllowedError" || name === "PermissionDeniedError") return true;
    const msg = String(err?.message || err || "").toLowerCase();
    return msg.includes("permission")
      || msg.includes("notallowed")
      || msg.includes("denied")
      || msg.includes("not allowed");
  }

  function stopLocalStream() {
    if (!localStream) return;
    localStream.getTracks().forEach((track) => {
      try {
        track.onended = null;
        track.stop();
      } catch (_) {
        /* ignore */
      }
    });
    localStream = null;
    if (localVideo) localVideo.srcObject = null;
  }

  function watchLocalTracks() {
    if (!localStream) return;
    localStream.getTracks().forEach((track) => {
      track.onended = () => {
        if (!hasLiveMedia() && (inQueue || currentRoomId)) {
          handleMediaLost();
        }
      };
    });
  }

  function setMediaHint(message) {
    if (!mediaPermissionHint) return;
    if (message) {
      mediaPermissionHint.hidden = false;
      mediaPermissionHint.textContent = message;
    } else {
      mediaPermissionHint.hidden = true;
      mediaPermissionHint.textContent = "";
    }
  }

  function showMediaPermissionGate({ resumeJoin = false } = {}) {
    resumeJoinAfterMedia = !!resumeJoin;
    mediaGateOpen = true;
    setMediaHint("");
    setStatus(t.mediaRequired || "Camera/microphone permission is required.");
    setControls({ queued: false, matched: false });
    if (mediaPermissionModal) {
      mediaPermissionModal.show();
    } else {
      toast.error(t.mediaRequired || "Camera/microphone permission is required.");
    }
  }

  function hideMediaPermissionGate() {
    mediaGateOpen = false;
    resumeJoinAfterMedia = false;
    setMediaHint("");
    mediaPermissionModal?.hide();
  }

  function handleMediaLost() {
    const wasActive = inQueue || !!currentRoomId;
    if (socket?.connected) {
      socket.emit("queue:leave");
      if (currentRoomId) {
        socket.emit("room:leave", { roomId: currentRoomId });
      }
    }
    leaveCurrentRoom({ notifyServer: false });
    inQueue = false;
    stopLocalStream();
    showMediaPermissionGate({ resumeJoin: wasActive });
  }

  async function ensureMedia({ force = false } = {}) {
    if (!force && hasLiveMedia()) return localStream;

    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error("MEDIA_UNSUPPORTED");
    }

    if (localStream) {
      stopLocalStream();
    }

    localStream = await navigator.mediaDevices.getUserMedia({
      video: buildVideoConstraints(preferredFacingMode),
      audio: {
        echoCancellation: true,
        noiseSuppression: true
      }
    });

    watchLocalTracks();

    if (localVideo) {
      localVideo.srcObject = localStream;
      localVideo.muted = true;
      localVideo.playsInline = true;
      syncLocalVideoMirror();
      try {
        await localVideo.play();
      } catch (_) {
        /* autoplay policies */
      }
    }

    localStream.getAudioTracks().forEach((tr) => { tr.enabled = micEnabled; });
    localStream.getVideoTracks().forEach((tr) => { tr.enabled = camEnabled; });
    syncMediaToggleUi();

    return localStream;
  }

  function destroyPeerConnection() {
    if (!pc) return;
    try {
      pc.ontrack = null;
      pc.onicecandidate = null;
      pc.onconnectionstatechange = null;
      pc.oniceconnectionstatechange = null;
      pc.close();
    } catch (_) {
      /* ignore */
    }
    pc = null;
    pendingRemoteIce = [];
  }

  async function flushPendingIce() {
    if (!pc || !pc.remoteDescription) return;
    const batch = pendingRemoteIce.splice(0, pendingRemoteIce.length);
    for (const candidate of batch) {
      try {
        await pc.addIceCandidate(candidate);
      } catch (err) {
        console.warn("Buffered ICE failed", err);
      }
    }
  }

  function createPeerConnection() {
    destroyPeerConnection();

    pc = new RTCPeerConnection({
      iceServers: buildIceServers()
    });

    localStream.getTracks().forEach((track) => {
      pc.addTrack(track, localStream);
    });

    pc.ontrack = async (event) => {
      const [stream] = event.streams;
      if (!remoteVideo) return;
      remoteVideo.srcObject = stream || new MediaStream([event.track]);
      remoteVideo.playsInline = true;
      setRemoteVisible(true);
      try {
        await remoteVideo.play();
      } catch (_) {
        /* user gesture may be required on some browsers */
      }
    };

    pc.onicecandidate = (event) => {
      if (!event.candidate || !socket || !currentRoomId) return;
      socket.emit("webrtc:ice-candidate", {
        roomId: currentRoomId,
        candidate: event.candidate
      });
    };

    pc.onconnectionstatechange = () => {
      const state = pc?.connectionState;
      if (state === "connected") {
        toast.success(t.toastConnected || "Video connection established.");
        setStatus(format(t.matched || "Matched with {0}", peerName || "partner"));
      } else if (state === "failed") {
        toast.error(t.toastFailed || "Connection failed. Trying next partner...");
        void skipNext({ silent: true });
      } else if (state === "disconnected") {
        toast.warn(t.toastDisconnected || "Connection unstable...");
      }
    };
  }

  async function leaveCurrentRoom({ notifyServer = true, complete = true, askRating = false } = {}) {
    if (complete) {
      await completeMatchOnServer();
    }
    if (askRating) {
      openRatingIfNeeded();
    }
    if (notifyServer && socket && currentRoomId) {
      socket.emit("room:leave", { roomId: currentRoomId });
    }
    currentRoomId = null;
    peerName = null;
    peerSocketId = null;
    peerUserId = null;
    peerGuestSessionId = null;
    isOfferer = false;
    matchStartedAt = null;
    if (remoteVideo) remoteVideo.srcObject = null;
    setRemoteVisible(false);
    destroyPeerConnection();
    clearChat();
    setChatEnabled(false);
    if (socialActions) socialActions.hidden = true;
  }

  function connectSocket() {
    if (socket?.connected) return Promise.resolve(socket);
    if (socket) {
      socket.connect();
      return Promise.resolve(socket);
    }

    return new Promise((resolve, reject) => {
      if (typeof io !== "function") {
        reject(new Error("SOCKET_IO_MISSING"));
        return;
      }

      socket = io(cfg.signalingUrl, {
        transports: ["websocket", "polling"],
        upgrade: true,
        rememberUpgrade: true,
        withCredentials: true,
        reconnection: true,
        reconnectionAttempts: 12,
        reconnectionDelay: 1000,
        reconnectionDelayMax: 8000,
        timeout: 15000
      });

      const onConnect = () => {
        cleanupConnectListeners();
        setStatus(t.connected || "Connected.");
        toast.info(t.toastSignalingConnected || "Connected to matchmaking.");
        resolve(socket);
      };

      const onConnectError = (err) => {
        cleanupConnectListeners();
        reject(err);
      };

      const cleanupConnectListeners = () => {
        socket.off("connect", onConnect);
        socket.off("connect_error", onConnectError);
      };

      socket.on("connect", onConnect);
      socket.on("connect_error", onConnectError);

      socket.on("disconnect", (reason) => {
        inQueue = false;
        setControls({ queued: false, matched: !!currentRoomId });
        if (reason !== "io client disconnect") {
          toast.warn(t.toastSignalingLost || "Signaling disconnected.");
          setStatus(t.toastSignalingLost || "Signaling disconnected.");
        }
      });

      socket.on("queue:waiting", (payload = {}) => {
        inQueue = true;
        setControls({ queued: true, matched: false });
        const eta = payload.estimatedWaitSec
          ? format(t.queueEta || "Est. wait ~{0}s · {1} in queue", payload.estimatedWaitSec, payload.positionHint ?? "?")
          : (t.waiting || "Waiting for a match...");
        setStatus(eta);
        if (queueHintText) {
          queueHintText.textContent = payload.tip || (t.waiting || "Waiting for a match...");
        }
        setRemoteVisible(false);
      });

      socket.on("queue:left", () => {
        inQueue = false;
        if (!currentRoomId) {
          setControls({ queued: false, matched: false });
          setStatus(t.left || "Left the queue.");
        }
      });

      socket.on("match:found", async (payload) => {
        try {
          if (mediaGateOpen || !hasLiveMedia()) {
            if (socket?.connected && payload?.roomId) {
              socket.emit("room:leave", { roomId: payload.roomId });
            }
            showMediaPermissionGate({ resumeJoin: true });
            return;
          }

          suppressAutoRequeue = false;
          currentRoomId = payload.roomId;
          peerName = payload.peerName;
          peerSocketId = payload.peerSocketId || null;
          peerUserId = payload.peerUserId || null;
          peerGuestSessionId = payload.peerGuestSessionId || null;
          isOfferer = !!payload.isOfferer;
          inQueue = false;
          matchStartedAt = Date.now();
          cfg.rematchWithUserId = null;
          clearChat();
          showTopic(Math.floor(Math.random() * Math.max(topics.length, 1)));
          setControls({ queued: false, matched: true });
          setStatus(format(t.matched || "Matched with {0}. Room: {1}", peerName, currentRoomId));
          toast.success(format(t.toastMatched || "Matched with {0}", peerName));
          notifyMatch("LinguaTalk", format(t.toastMatched || "Matched with {0}", peerName));

          await ensureMedia();
          createPeerConnection();

          if (isOfferer) {
            const offer = await pc.createOffer({
              offerToReceiveAudio: true,
              offerToReceiveVideo: true
            });
            await pc.setLocalDescription(offer);
            socket.emit("webrtc:offer", { roomId: currentRoomId, sdp: pc.localDescription });
          }
        } catch (err) {
          console.error(err);
          if (isMediaPermissionError(err)) {
            handleMediaLost();
            return;
          }
          toast.error(t.toastMatchError || "Could not start the call.");
        }
      });

      socket.on("webrtc:offer", async ({ sdp, roomId }) => {
        try {
          if (roomId && currentRoomId && roomId !== currentRoomId) return;
          if (mediaGateOpen) return;
          if (!pc) {
            await ensureMedia();
            createPeerConnection();
          }
          await pc.setRemoteDescription(sdp);
          await flushPendingIce();
          const answer = await pc.createAnswer();
          await pc.setLocalDescription(answer);
          socket.emit("webrtc:answer", {
            roomId: currentRoomId || roomId,
            sdp: pc.localDescription
          });
        } catch (err) {
          console.error(err);
          if (isMediaPermissionError(err)) {
            handleMediaLost();
            return;
          }
          toast.error(t.toastSignalError || "Signaling error during offer.");
        }
      });

      socket.on("webrtc:answer", async ({ sdp, roomId }) => {
        try {
          if (roomId && currentRoomId && roomId !== currentRoomId) return;
          if (!pc) return;
          await pc.setRemoteDescription(sdp);
          await flushPendingIce();
        } catch (err) {
          console.error(err);
          toast.error(t.toastSignalError || "Signaling error during answer.");
        }
      });

      socket.on("webrtc:ice-candidate", async ({ candidate, roomId }) => {
        if (!candidate) return;
        if (roomId && currentRoomId && roomId !== currentRoomId) return;
        if (!pc || !pc.remoteDescription) {
          if (pendingRemoteIce.length >= MAX_PENDING_ICE) {
            pendingRemoteIce.shift();
          }
          pendingRemoteIce.push(candidate);
          return;
        }
        try {
          await pc.addIceCandidate(candidate);
        } catch (err) {
          console.warn("ICE candidate error", err);
        }
      });

      socket.on("chat:message", (msg) => {
        if (!msg || !currentRoomId || (msg.roomId && msg.roomId !== currentRoomId)) return;
        const mine = msg.fromSocketId === socket.id;
        appendChatMessage({
          text: msg.text,
          fromName: msg.fromName,
          fromSocketId: msg.fromSocketId,
          mine
        });
      });

      socket.on("match:peer-left", () => {
        toast.warn(t.toastPeerLeft || t.peerLeft || "The other person left.");
        setStatus(t.peerLeft || "The other person left.");
        leaveCurrentRoom({ notifyServer: false });
        setControls({ queued: false, matched: false });
        if (!suppressAutoRequeue) {
          void joinQueue({ fromPeerLeft: true });
        }
        suppressAutoRequeue = false;
      });

      socket.on("moderation:call-ended", ({ reason }) => {
        suppressAutoRequeue = true;
        leaveCurrentRoom({ notifyServer: false });
        setControls({ queued: false, matched: false });
        const msg = reason === "peer_reported"
          ? (t.toastReportEnded || "Call ended after your report.")
          : (t.toastCallEndedModeration || "Call ended by moderation.");
        toast.warn(msg);
        setStatus(msg);
      });

      socket.on("moderation:banned", () => {
        suppressAutoRequeue = true;
        leaveCurrentRoom({ notifyServer: false });
        setControls({ queued: false, matched: false });
        toast.error(t.toastYouBanned || "You were banned. Session ended.");
        setStatus(t.toastYouBanned || "You were banned.");
      });

      socket.on("error:message", ({ message }) => {
        toast.error(message || t.toastGenericError || "Something went wrong.");
      });
    });
  }

  async function joinQueue({ fromPeerLeft = false } = {}) {
    if (busy || mediaGateOpen) return;
    busy = true;
    setControls({ queued: true, matched: false });

    try {
      await ensureMedia();
      await connectSocket();

      if (currentRoomId) {
        leaveCurrentRoom();
      }

      const languageCode = getSelectedLanguage();
      socket.emit("queue:join", queuePayload());

      inQueue = true;
      setStatus(t.queued || "Added to the queue...");
      if (!fromPeerLeft) {
        toast.info(format(t.toastQueued || "Queued for {0}", languageCode.toUpperCase()));
      }
      setControls({ queued: true, matched: false });
    } catch (err) {
      console.error(err);
      inQueue = false;
      setControls({ queued: false, matched: false });

      if (err?.message === "MEDIA_UNSUPPORTED") {
        toast.error(t.toastMediaUnsupported || "Camera/microphone is not supported.");
        setStatus(t.toastMediaUnsupported || "Media unsupported.");
      } else if (isMediaPermissionError(err)) {
        showMediaPermissionGate({ resumeJoin: true });
      } else if (err?.message === "SOCKET_IO_MISSING") {
        toast.error(t.toastSocketMissing || "Socket.io failed to load.");
      } else {
        toast.error(t.toastSignalingFailed || "Could not reach matchmaking server.");
        setStatus(t.toastSignalingFailed || "Matchmaking unavailable.");
      }
      throw err;
    } finally {
      busy = false;
    }
  }

  async function skipNext({ silent = false } = {}) {
    if (busy || mediaGateOpen) return;

    if (!silent) {
      setStatus(t.next || "Skipping to next partner...");
      toast.info(t.next || "Skipping to next partner...");
    }

    if (socket?.connected) {
      socket.emit("queue:leave");
      if (currentRoomId) {
        socket.emit("room:leave", { roomId: currentRoomId });
      }
    }

    leaveCurrentRoom();
    inQueue = false;
    setControls({ queued: false, matched: false });

    // Tek seferde yeniden kuyruğa gir (çift join yok)
    await joinQueue({ fromPeerLeft: true });
  }

  async function leaveQueueAndCall() {
    if (socket?.connected) {
      socket.emit("queue:leave");
    }
    await leaveCurrentRoom({ notifyServer: true, complete: true, askRating: !!currentRoomId });
    inQueue = false;
    setControls({ queued: false, matched: false });
    setStatus(t.ready || "Ready.");
    toast.info(t.toastLeft || "You left the session.");
  }

  async function submitReport(event) {
    event.preventDefault();
    if (!currentRoomId || (!peerSocketId && !peerUserId && !peerGuestSessionId)) {
      toast.error(t.toastReportNoPeer || "No active peer to report.");
      return;
    }

    const reasonCode = reportForm?.querySelector('[name="reasonCode"]')?.value || "inappropriate";
    const details = reportForm?.querySelector('[name="details"]')?.value || "";

    try {
      suppressAutoRequeue = true;
      const res = await fetch("/api/reports/live", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Accept": "application/json"
        },
        credentials: "same-origin",
        body: JSON.stringify({
          reasonCode,
          details,
          roomId: currentRoomId,
          reportedPeerSocketId: peerSocketId,
          reportedPeerDisplayName: peerName,
          reportedUserId: peerUserId,
          reportedGuestSessionId: peerGuestSessionId
        })
      });

      const data = await res.json().catch(() => ({}));
      if (res.status === 429) {
        toast.warn(data.error || t.toastReportRate || "Please wait before reporting again.");
        return;
      }
      if (!res.ok) {
        toast.error(data.error || t.toastReportFailed || "Report failed.");
        return;
      }

      reportModal?.hide();
      leaveCurrentRoom({ notifyServer: false });
      setControls({ queued: false, matched: false });
      toast.success(data.message || t.toastReportSent || "Report submitted. Call ended.");
      setStatus(data.message || t.toastReportSent || "Report submitted.");
      if (reportForm) reportForm.reset();
    } catch (err) {
      console.error(err);
      toast.error(t.toastReportFailed || "Report failed.");
    }
  }

  mediaPermissionEnableBtn?.addEventListener("click", () => {
    if (busy) return;
    busy = true;
    if (mediaPermissionEnableBtn) mediaPermissionEnableBtn.disabled = true;
    setMediaHint("");

    void (async () => {
      try {
        await ensureMedia({ force: true });
        const shouldResume = resumeJoinAfterMedia;
        hideMediaPermissionGate();
        setControls({ queued: false, matched: false });
        if (shouldResume) {
          await joinQueue({ fromPeerLeft: true });
        } else {
          setStatus(t.ready || "Ready.");
        }
      } catch (err) {
        console.error(err);
        if (err?.message === "MEDIA_UNSUPPORTED") {
          setMediaHint(t.toastMediaUnsupported || "Camera/microphone is not supported.");
        } else {
          setMediaHint(
            t.mediaGateStillBlocked
            || "Permission is still blocked. Enable microphone and camera in your browser settings, then try again."
          );
        }
        setStatus(t.mediaRequired || "Camera/microphone permission is required.");
      } finally {
        busy = false;
        if (mediaPermissionEnableBtn) mediaPermissionEnableBtn.disabled = false;
      }
    })();
  });

  mediaPermissionCancelBtn?.addEventListener("click", () => {
    hideMediaPermissionGate();
    inQueue = false;
    setControls({ queued: false, matched: false });
    setStatus(t.ready || "Ready.");
  });

  joinBtn?.addEventListener("click", () => {
    if (mediaGateOpen) return;
    void joinQueue().catch(() => {});
  });

  nextBtn?.addEventListener("click", () => {
    if (mediaGateOpen) return;
    void skipNext().catch(() => {});
  });

  leaveBtn?.addEventListener("click", () => {
    if (mediaGateOpen) return;
    void leaveQueueAndCall();
  });

  backToLobbyBtn?.addEventListener("click", () => {
    if (mediaGateOpen) return;
    void leaveQueueAndCall().then(() => {
      setStatus(t.ready || "Ready.");
      toast.info(t.toastBackToLobby || t.toastLeft || "Back to lobby.");
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  });

  toggleMicBtn?.addEventListener("click", () => {
    micEnabled = !micEnabled;
    localStream?.getAudioTracks().forEach((tr) => { tr.enabled = micEnabled; });
    syncMediaToggleUi();
  });

  toggleCamBtn?.addEventListener("click", () => {
    camEnabled = !camEnabled;
    localStream?.getVideoTracks().forEach((tr) => { tr.enabled = camEnabled; });
    syncMediaToggleUi();
  });

  switchCamBtn?.addEventListener("click", () => {
    void switchCamera();
  });

  nextTopicBtn?.addEventListener("click", () => showTopic(topicIndex + 1));

  friendBtn?.addEventListener("click", () => {
    if (!peerUserId || cfg.isGuest) return;
    void fetch("/api/social/friends/request", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ userId: peerUserId })
    }).then((res) => {
      if (res.ok) toast.success(t.toastFriendSent || "Friend request sent.");
      else toast.error(t.toastGenericError || "Could not send request.");
    }).catch(() => toast.error(t.toastGenericError || "Could not send request."));
  });

  blockBtn?.addEventListener("click", () => {
    if (cfg.isGuest) return;
    void fetch("/api/social/block", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({
        userId: peerUserId || null,
        guestSessionId: peerGuestSessionId || null,
        reason: "blocked-from-lobby"
      })
    }).then(async (res) => {
      if (!res.ok) {
        toast.error(t.toastGenericError || "Could not block.");
        return;
      }
      if (peerUserId) {
        cfg.blockedUserIds = [...(cfg.blockedUserIds || []), peerUserId];
      }
      toast.warn(t.toastBlocked || "User blocked.");
      await leaveQueueAndCall();
    }).catch(() => toast.error(t.toastGenericError || "Could not block."));
  });

  document.querySelectorAll(".rating-star").forEach((btn) => {
    btn.addEventListener("click", () => {
      const rating = Number(btn.getAttribute("data-rating") || 0);
      const roomId = currentRoomId;
      // rating modal may open while room id still known via dataset
      const rateRoom = ratingModalEl?.dataset.roomId || roomId;
      if (!rateRoom || !rating) return;
      void fetch(`/api/social/matches/${encodeURIComponent(rateRoom)}/rate`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        credentials: "same-origin",
        body: JSON.stringify({ rating })
      }).then((res) => {
        if (res.ok) toast.success(t.toastRated || "Thanks for rating!");
        ratingModal?.hide();
      });
    });
  });

  reportBtn?.addEventListener("click", () => {
    openReportModal();
  });

  reportFab?.addEventListener("click", () => {
    openReportModal();
  });

  reportForm?.addEventListener("submit", (e) => {
    void submitReport(e);
  });

  chatForm?.addEventListener("submit", (e) => {
    e.preventDefault();
    if (!socket?.connected || !currentRoomId || !chatInput) return;
    const text = chatInput.value.trim();
    if (!text) return;
    socket.emit("chat:message", { text, roomId: currentRoomId });
    chatInput.value = "";
    chatInput.focus();
  });

  window.addEventListener("beforeunload", () => {
    if (socket?.connected) {
      socket.emit("queue:leave");
      if (currentRoomId) socket.emit("room:leave", { roomId: currentRoomId });
    }
  });

  setRemoteVisible(false);
  clearChat();
  setControls({ queued: false, matched: false });
  showTopic(0);
  syncMediaToggleUi();
  if (cfg.browserNotifications && typeof Notification !== "undefined" && Notification.permission === "default") {
    Notification.requestPermission().catch(() => {});
  }

  if (cfg.autoStart) {
    setTimeout(() => {
      void joinQueue().catch(() => {});
    }, 300);
  }
})();
