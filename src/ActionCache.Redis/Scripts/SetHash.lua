local namespace = KEYS[1]
local key = KEYS[2]
local absoluteExpiration = tonumber(ARGV[2])
local slidingExpiration = tonumber(ARGV[3])
local ttl = tonumber(ARGV[4])

redis.call("HSET",  namespace .. ":" .. key, "VALUE", ARGV[1], "ABSOLUTE_EXPIRATION", absoluteExpiration, "SLIDING_EXPIRATION", slidingExpiration)
redis.call("ZADD", namespace, absoluteExpiration, key)

if ttl > 0 then 
    redis.call("PEXPIRE", namespace .. ":" .. key, ttl) 
end