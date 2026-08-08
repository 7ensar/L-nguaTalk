export class RoomRegistry {
  constructor() {
    /** @type {Map<string, { languageCode: string, members: Set<string>, createdAt: number }>} */
    this.rooms = new Map();
    /** @type {Map<string, string>} socketId -> roomId */
    this.socketRoom = new Map();
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
