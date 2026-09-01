redis.call("UNLINK", KEYS[1] .. ":" .. KEYS[2])
redis.call("ZREM", KEYS[1], KEYS[2])