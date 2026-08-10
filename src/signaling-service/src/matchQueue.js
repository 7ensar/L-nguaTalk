/**
 * Dil bazlı FIFO eşleşme kuyruğu.
 * - socketId indeksi ile O(1) silme
 * - max uzunluk + TTL ile bayat bekleyicileri temizleme
 */
export class MatchQueue {
  constructor({
    maxPerLanguage = 500,
    maxWaitMs = 10 * 60_000
  } = {}) {
    /** @type {Map<string, Array<{ socketId: string, displayName: string, languageCode: string, joinedAt: number, userId?: string|null, guestSessionId?: string|null }>>} */
    this.queues = new Map();
    /** @type {Map<string, string>} socketId -> languageCode */
    this.socketLang = new Map();
    this.maxPerLanguage = maxPerLanguage;
    this.maxWaitMs = maxWaitMs;
  }

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

    const row = { ...entry, languageCode: key, joinedAt: Date.now() };
    list.push(row);
    this.socketLang.set(entry.socketId, key);
    return this.tryMatch(key);
  }

  tryMatch(languageCode) {
    const list = this.queues.get(languageCode) || [];
    if (list.length < 2) {
      return null;
    }

    const a = list.shift();
    const b = list.shift();
    if (a) this.socketLang.delete(a.socketId);
    if (b) this.socketLang.delete(b.socketId);
    if (list.length === 0) {
      this.queues.delete(languageCode);
    }
    return { a, b, languageCode };
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

  /**
   * Bayat kuyruk kayıtlarını temizler.
   * @returns {string[]} süre aşımıyla çıkarılan socket id'leri
   */
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
}
