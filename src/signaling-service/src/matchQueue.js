/**
 * Dil bazlı eşleşme kuyruğu.
 * - blok listesi
 * - seviye / cinsiyet / ilgi skoru
 * - premium öncelik
 */
export class MatchQueue {
  constructor({
    maxPerLanguage = 500,
    maxWaitMs = 10 * 60_000
  } = {}) {
    /** @type {Map<string, Array<QueueEntry>>} */
    this.queues = new Map();
    /** @type {Map<string, string>} */
    this.socketLang = new Map();
    this.maxPerLanguage = maxPerLanguage;
    this.maxWaitMs = maxWaitMs;
  }

  /**
   * @typedef {{
   *  socketId: string,
   *  displayName: string,
   *  languageCode: string,
   *  joinedAt: number,
   *  userId?: string|null,
   *  guestSessionId?: string|null,
   *  languageLevel?: number|null,
   *  gender?: number|null,
   *  preferredPartnerGender?: number|null,
   *  interests?: string[],
   *  preferSimilarLevel?: boolean,
   *  preferSharedInterests?: boolean,
   *  isPremium?: boolean,
   *  blockedUserIds?: string[],
   *  rematchWithUserId?: string|null
   * }} QueueEntry
   */

  /** @param {QueueEntry} entry */
  enqueue(entry) {
    const key = entry.languageCode.toLowerCase();
    if (this.socketLang.has(entry.socketId)) {
      return null;
    }

    if (!this.queues.has(key)) {
      this.queues.set(key, []);
    }

    const list = this.queues.get(key);
    if (list.length >= this.maxPerLanguage) {
      return { overflow: true };
    }

    const row = {
      ...entry,
      languageCode: key,
      joinedAt: Date.now(),
      interests: Array.isArray(entry.interests) ? entry.interests.map((x) => String(x).toLowerCase()) : [],
      blockedUserIds: Array.isArray(entry.blockedUserIds) ? entry.blockedUserIds.map(String) : [],
      isPremium: !!entry.isPremium,
      preferSimilarLevel: entry.preferSimilarLevel !== false,
      preferSharedInterests: entry.preferSharedInterests !== false
    };

    // Premium kullanıcıları kuyruğun önüne yakın tut
    if (row.isPremium) {
      const firstNonPremium = list.findIndex((x) => !x.isPremium);
      if (firstNonPremium < 0) {
        list.push(row);
      } else {
        list.splice(firstNonPremium, 0, row);
      }
    } else {
      list.push(row);
    }

    this.socketLang.set(entry.socketId, key);
    return this.tryMatch(key);
  }

  tryMatch(languageCode) {
    const list = this.queues.get(languageCode) || [];
    if (list.length < 2) {
      return null;
    }

    for (let i = 0; i < list.length; i += 1) {
      const a = list[i];
      let bestIdx = -1;
      let bestScore = -1;

      for (let j = i + 1; j < list.length; j += 1) {
        const b = list[j];
        if (!this.canPair(a, b)) {
          continue;
        }
        const score = this.scorePair(a, b);
        if (score > bestScore) {
          bestScore = score;
          bestIdx = j;
        }
      }

      if (bestIdx < 0) {
        continue;
      }

      const b = list[bestIdx];
      list.splice(bestIdx, 1);
      list.splice(i, 1);
      this.socketLang.delete(a.socketId);
      this.socketLang.delete(b.socketId);
      if (list.length === 0) {
        this.queues.delete(languageCode);
      }
      return { a, b, languageCode, score: bestScore };
    }

    return null;
  }

  /** @param {QueueEntry} a @param {QueueEntry} b */
  canPair(a, b) {
    if (a.socketId === b.socketId) return false;
    if (a.userId && b.userId && a.userId === b.userId) return false;

    if (a.userId && (b.blockedUserIds || []).includes(a.userId)) return false;
    if (b.userId && (a.blockedUserIds || []).includes(b.userId)) return false;

    // Rematch isteği: sadece hedef kullanıcıyla eşleş
    if (a.rematchWithUserId && a.rematchWithUserId !== b.userId) return false;
    if (b.rematchWithUserId && b.rematchWithUserId !== a.userId) return false;

    if (a.preferredPartnerGender != null && b.gender != null
      && Number(a.preferredPartnerGender) !== Number(b.gender)
      && Number(a.preferredPartnerGender) !== 0) {
      return false;
    }
    if (b.preferredPartnerGender != null && a.gender != null
      && Number(b.preferredPartnerGender) !== Number(a.gender)
      && Number(b.preferredPartnerGender) !== 0) {
      return false;
    }

    return true;
  }

  /** @param {QueueEntry} a @param {QueueEntry} b */
  scorePair(a, b) {
    let score = 10;
    if (a.isPremium || b.isPremium) score += 5;

    if (a.rematchWithUserId && a.rematchWithUserId === b.userId) score += 100;
    if (b.rematchWithUserId && b.rematchWithUserId === a.userId) score += 100;

    if (a.languageLevel != null && b.languageLevel != null) {
      const diff = Math.abs(Number(a.languageLevel) - Number(b.languageLevel));
      if (a.preferSimilarLevel || b.preferSimilarLevel) {
        score += Math.max(0, 6 - diff * 2);
      }
    }

    if (a.preferSharedInterests || b.preferSharedInterests) {
      const setA = new Set(a.interests || []);
      let shared = 0;
      for (const tag of b.interests || []) {
        if (setA.has(tag)) shared += 1;
      }
      score += Math.min(8, shared * 2);
    }

    // Daha uzun bekleyenleri hafifçe önceliklendir
    const waitBonus = Math.min(5, Math.floor((Date.now() - Math.min(a.joinedAt, b.joinedAt)) / 30_000));
    score += waitBonus;
    return score;
  }

  remove(socketId) {
    const lang = this.socketLang.get(socketId);
    if (!lang) {
      return false;
    }

    const list = this.queues.get(lang);
    this.socketLang.delete(socketId);
    if (!list) {
      return true;
    }

    const idx = list.findIndex((x) => x.socketId === socketId);
    if (idx >= 0) {
      list.splice(idx, 1);
    }
    if (list.length === 0) {
      this.queues.delete(lang);
    }
    return idx >= 0;
  }

  pruneStale(now = Date.now()) {
    /** @type {string[]} */
    const expired = [];
    for (const [lang, list] of this.queues.entries()) {
      const kept = [];
      for (const entry of list) {
        if (now - entry.joinedAt > this.maxWaitMs) {
          this.socketLang.delete(entry.socketId);
          expired.push(entry.socketId);
        } else {
          kept.push(entry);
        }
      }
      if (kept.length === 0) {
        this.queues.delete(lang);
      } else {
        this.queues.set(lang, kept);
      }
    }
    return expired;
  }

  size(languageCode) {
    return (this.queues.get(languageCode.toLowerCase()) || []).length;
  }

  totalSize() {
    return this.socketLang.size;
  }

  snapshotByLanguage() {
    /** @type {Record<string, number>} */
    const result = {};
    for (const [lang, list] of this.queues.entries()) {
      result[lang] = list.length;
    }
    return result;
  }

  /** Ortalama bekleme ipucu (ms). */
  estimatedWaitMs(languageCode) {
    const list = this.queues.get(languageCode.toLowerCase()) || [];
    if (list.length === 0) return 0;
    if (list.length === 1) return 45_000;
    return 8_000;
  }
}
