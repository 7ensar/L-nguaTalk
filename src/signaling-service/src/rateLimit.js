/**
 * Basit bellek içi rate limiter (IP / socket bazlı).
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
      const retryAfterMs = Math.max(0, list[0] + this.windowMs - now);
      this.hits.set(key, list);
      return { allowed: false, retryAfterMs };
    }
    list.push(now);
    this.hits.set(key, list);
    return { allowed: true, retryAfterMs: 0 };
  }
}
