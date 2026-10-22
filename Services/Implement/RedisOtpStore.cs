using System.Text.Json;
using SportsCenterAPI.Helpers;
using StackExchange.Redis;

namespace SportsCenterAPI.Services.Implement;

public class RedisOtpStore(IConnectionMultiplexer redis)
{
    // Một hash nhỏ cho mỗi OTP: mã đã băm, dữ liệu chờ, số lần nhập sai.
    // Script giúp ghi dữ liệu và đặt thời hạn cùng nhau.
    private const string SaveScript = """
        redis.call('HSET', KEYS[1],
            'otp_hash', ARGV[1], 'payload', ARGV[2], 'attempts', 0)
        redis.call('EXPIRE', KEYS[1], 300)
        return 1
        """;

    // Redis chạy script liền mạch: hai request không dùng được cùng một OTP.
    private const string VerifyScript = """
        local expected = redis.call('HGET', KEYS[1], 'otp_hash')
        if not expected then return nil end

        if expected ~= ARGV[1] then
            local attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
            if attempts >= 5 then redis.call('DEL', KEYS[1]) end
            return nil
        end

        local payload = redis.call('HGET', KEYS[1], 'payload')
        redis.call('DEL', KEYS[1])
        return payload
        """;

    public async Task SaveAsync<T>(string key, string otpHash, T value)
    {
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                SaveScript, [(RedisKey)key], [otpHash, JsonSerializer.Serialize(value)]);
        }
        catch (RedisException)
        {
            throw new BusinessException(503, "Chưa lưu được OTP. Vui lòng thử lại sau.");
        }
    }

    public async Task<T?> ConsumeAsync<T>(string key, string otpHash) where T : class
    {
        try
        {
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                VerifyScript, [(RedisKey)key], [otpHash]);

            var payload = (string?)result;
            return payload == null ? null : JsonSerializer.Deserialize<T>(payload);
        }
        catch (RedisException)
        {
            throw new BusinessException(503, "Chưa kiểm tra được OTP. Vui lòng thử lại sau.");
        }
    }
}
