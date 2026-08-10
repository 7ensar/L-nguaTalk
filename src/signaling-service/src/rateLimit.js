/**
 * Bellek içi sliding-window rate limiter + periyodik temizlik.
 */
export class RateLimiter {
  /**
   * @param {{ windowMs: number, max: number }} opts
   */
  constructor({ windowMs, max }) {
    this.windowMs = windowMs;
    this.max = max;
    /** @type {Map<string, number[]>} */
    this.hits = new Map();
  }

  /**
   * @param {string} key
   * @returns {{ allowed: boolean, retryAfterMs: number }}
   */
  check(key) {
    const now = Date.now();
    const windowStart = now - this.windowMs;
    const list = (this.hits.get(key) || []).filter((t) => t >= windowStart);
    if (list.length >= this.max) {
      const retryAfterMs = Math.max(0, (list[0] || now) + this.windowMs - now);
      if (list.length === 0) {
        this.hits.delete(key);
      } else {
        this.hits.set(key, list);
      }
      return { allowed: false, retryAfterMs };
    }
    list.push(now);
    this.hits.set(key, list);
    return { allowed: true, retryAfterMs: 0 };
  }

  /** Süresi dolmuş anahtarları sil (bellek sızıntısını önler). */
  prune() {
    const now = Date.now();
    const windowStart = now - this.windowMs;
    for (const [key, list] of this.hits.entries()) {
      const kept = list.filter((t) => t >= windowStart);
      if (kept.length === 0) {
        this.hits.delete(key);
      } else if (kept.length !== list.length) {
        this.hits.set(key, kept);
      }
    }
  }
}
