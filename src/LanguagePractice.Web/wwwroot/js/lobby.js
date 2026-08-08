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
  const reportForm = document.getElementById("reportForm");
  const reportModalEl = document.getElementById("reportModal");
  const localVideo = document.getElementById("localVideo");
  const remoteVideo = document.getElementById("remoteVideo");
  const videoEmpty = document.getElementById("videoEmpty");
  const chatMessages = document.getElementById("chatMessages");
  const chatForm = document.getElementById("chatForm");
  const chatInput = document.getElementById("chatInput");
  const chatSendBtn = document.getElementById("chatSendBtn");
  const chatPeerLabel = document.getElementById("chatPeerLabel");

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
  /** @type {RTCIceCandidateInit[]} */
  let pendingRemoteIce = [];
  const reportModal = reportModalEl && window.bootstrap
    ? bootstrap.Modal.getOrCreateInstance(reportModalEl)
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
    chatMessages.scrollTop = chatMessages.scrollHeight;
  }

  function setControls({ queued, matched }) {
    if (joinBtn) joinBtn.disabled = !!queued || !!matched || busy;
    if (leaveBtn) leaveBtn.disabled = !(queued || matched);
    if (nextBtn) nextBtn.disabled = busy;
    if (reportBtn) reportBtn.disabled = !matched;
    setChatEnabled(!!matched);
  }

  async function ensureMedia() {
    if (localStream) return localStream;

    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error("MEDIA_UNSUPPORTED");
    }

    localStream = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: "user",
        width: { ideal: 1280 },
        height: { ideal: 720 }
      },
      audio: {
        echoCancellation: true,
        noiseSuppression: true
      }
    });

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
      iceServers: [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" }
      ]
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

  function leaveCurrentRoom({ notifyServer = true } = {}) {
    if (notifyServer && socket && currentRoomId) {
      socket.emit("room:leave", { roomId: currentRoomId });
    }
    currentRoomId = null;
    peerName = null;
    peerSocketId = null;
    peerUserId = null;
    peerGuestSessionId = null;
    isOfferer = false;
    if (remoteVideo) remoteVideo.srcObject = null;
    setRemoteVisible(false);
    destroyPeerConnection();
    clearChat();
    setChatEnabled(false);
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
        reconnection: true,
        reconnectionAttempts: 8,
        timeout: 10000
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

      socket.on("queue:waiting", () => {
        inQueue = true;
        setControls({ queued: true, matched: false });
        setStatus(t.waiting || "Waiting for a match...");
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
          suppressAutoRequeue = false;
          currentRoomId = payload.roomId;
          peerName = payload.peerName;
          peerSocketId = payload.peerSocketId || null;
          peerUserId = payload.peerUserId || null;
          peerGuestSessionId = payload.peerGuestSessionId || null;
          isOfferer = !!payload.isOfferer;
          inQueue = false;
          clearChat();
          setControls({ queued: false, matched: true });
          setStatus(format(t.matched || "Matched with {0}. Room: {1}", peerName, currentRoomId));
          toast.success(format(t.toastMatched || "Matched with {0}", peerName));

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
          toast.error(t.toastMatchError || "Could not start the call.");
        }
      });

      socket.on("webrtc:offer", async ({ sdp, roomId }) => {
        try {
          if (roomId && currentRoomId && roomId !== currentRoomId) return;
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
    if (busy) return;
    busy = true;
    setControls({ queued: true, matched: false });

    try {
      await ensureMedia();
      await connectSocket();

      if (currentRoomId) {
        leaveCurrentRoom();
      }

      const languageCode = getSelectedLanguage();
      socket.emit("queue:join", {
        displayName: cfg.displayName || "Guest",
        languageCode,
        userId: cfg.userId || null,
        guestSessionId: cfg.guestSessionId || null
      });

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
      } else if (String(err?.message || err).toLowerCase().includes("permission")) {
        toast.error(t.mediaRequired || "Camera/microphone permission is required.");
        setStatus(t.mediaRequired || "Camera/microphone permission is required.");
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
    if (busy) return;

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

  function leaveQueueAndCall() {
    if (socket?.connected) {
      socket.emit("queue:leave");
      if (currentRoomId) {
        socket.emit("room:leave", { roomId: currentRoomId });
      }
    }
    leaveCurrentRoom();
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

  joinBtn?.addEventListener("click", () => {
    void joinQueue().catch(() => {});
  });

  nextBtn?.addEventListener("click", () => {
    void skipNext().catch(() => {});
  });

  leaveBtn?.addEventListener("click", () => {
    leaveQueueAndCall();
  });

  reportBtn?.addEventListener("click", () => {
    if (!currentRoomId) {
      toast.warn(t.toastReportNoPeer || "No active peer to report.");
      return;
    }
    reportModal?.show();
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

  if (cfg.autoStart) {
    setTimeout(() => {
      void joinQueue().catch(() => {});
    }, 300);
  }
})();
