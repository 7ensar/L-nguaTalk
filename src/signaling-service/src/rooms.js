export class RoomRegistry {
  constructor({ maxRoomAgeMs = 3 * 60 * 60_000 } = {}) {
    /** @type {Map<string, { languageCode: string, members: Set<string>, createdAt: number }>} */
    this.rooms = new Map();
    /** @type {Map<string, string>} socketId -> roomId */
    this.socketRoom = new Map();
    this.maxRoomAgeMs = maxRoomAgeMs;
  }

  create(roomId, languageCode, socketIds) {
    this.rooms.set(roomId, {
      languageCode,
      members: new Set(socketIds),
      createdAt: Date.now()
    });

    for (const socketId of socketIds) {
      this.socketRoom.set(socketId, roomId);
    }
  }

  getRoomIdForSocket(socketId) {
    return this.socketRoom.get(socketId);
  }

  leave(socketId) {
    const roomId = this.socketRoom.get(socketId);
    if (!roomId) {
      return null;
    }

    const room = this.rooms.get(roomId);
    this.socketRoom.delete(socketId);

    if (!room) {
      return { roomId, remaining: [] };
    }

    room.members.delete(socketId);
    const remaining = [...room.members];

    if (remaining.length === 0) {
      this.rooms.delete(roomId);
    }

    return { roomId, remaining };
  }

  /**
   * Çok eski veya tek kişilik bayat odaları temizler.
   * @returns {{ roomId: string, members: string[] }[]}
   */
  pruneStale(now = Date.now()) {
    /** @type {{ roomId: string, members: string[] }[]} */
    const closed = [];
    for (const [roomId, room] of this.rooms.entries()) {
      const age = now - room.createdAt;
      const aloneTooLong = room.members.size <= 1 && age > 5 * 60_000;
      const tooOld = age > this.maxRoomAgeMs;
      if (!aloneTooLong && !tooOld) {
        continue;
      }

      const members = [...room.members];
      for (const memberId of members) {
        this.socketRoom.delete(memberId);
      }
      this.rooms.delete(roomId);
      closed.push({ roomId, members });
    }
    return closed;
  }

  /** Dil bazında görüşmedeki kişi sayısı. */
  snapshotByLanguage() {
    /** @type {Record<string, number>} */
    const result = {};
    for (const room of this.rooms.values()) {
      const lang = (room.languageCode || "en").toLowerCase();
      result[lang] = (result[lang] || 0) + room.members.size;
    }
    return result;
  }
}
