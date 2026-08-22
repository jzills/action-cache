local matches = redis.call("ZRANGE", KEYS[1], "-inf", "inf", "BYSCORE")
for _, key in ipairs(matches) do
    redis.call("UNLINK", KEYS[1] .. ":" .. key)
end
redis.call("ZREMRANGEBYSCORE", KEYS[1], "-inf", "inf")