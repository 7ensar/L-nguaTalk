/**
 * Dil bazlı basit FIFO eşleşme kuyruğu.
 * Production'da Redis / ayrı worker ile değiştirilebilir.
 */
export class MatchQueue {
  constructor() {
    /** @type {Map<string, Array<{ socketId: string, displayName: string, languageCode: string, joinedAt: number }>>} */
    this.queues = new Map();
  }

  enqueue(entry) {
    const key = entry.languageCode.toLowerCase();
    if (!this.queues.has(key)) {
      this.queues.set(key, []);
    }

    const list = this.queues.get(key);
    if (list.some((x) => x.socketId === entry.socketId)) {
      return null;
    }

    list.push({ ...entry, joinedAt: Date.now() });
    return this.tryMatch(key);
  }

  tryMatch(languageCode) {
    const list = this.queues.get(languageCode) || [];
    if (list.length < 2) {
      return null;
    }

    const a = list.shift();
    const b = list.shift();
    return { a, b, languageCode };
  }

  remove(socketId) {
    for (const [lang, list] of this.queues.entries()) {
      const idx = list.findIndex((x) => x.socketId === socketId);
      if (idx >= 0) {
        list.splice(idx, 1);
        if (list.length === 0) {
          this.queues.delete(lang);
        }
        return true;
      }
    }
    return false;
  }

  size(languageCode) {
    return (this.queues.get(languageCode.toLowerCase()) || []).length;
  }

  totalSize() {
    let total = 0;
    for (const list of this.queues.values()) {
      total += list.length;
    }
    return total;
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
