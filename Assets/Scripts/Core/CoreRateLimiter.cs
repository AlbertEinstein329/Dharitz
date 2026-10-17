using System.Collections.Generic;

namespace MyGame.Core
{
    public enum RateLimitResult
    {
        Allowed,
        Dropped, // supera el ritmo permitido: la peticion se ignora
        Kick     // reincidencia: el servidor debe expulsar al cliente
    }

    /// <summary>
    /// Limitador de ritmo por cubo de fichas (token bucket) con expulsion por reincidencia.
    /// Logica pura de la Capa 0: no depende de Unity; el reloj se inyecta (segundos) para poder probarlo.
    ///
    /// Cada clave (p. ej. un ClientId) tiene un cubo con <c>capacity</c> fichas que se rellena a
    /// <c>refillPerSecond</c>. Cada peticion gasta una ficha; sin fichas la peticion se descarta y cuenta
    /// como infraccion. Al acumular <c>maxStrikes</c> infracciones dentro de <c>strikeWindowSeconds</c> se pide la expulsion.
    /// </summary>
    public sealed class CoreRateLimiter
    {
        private sealed class Bucket
        {
            public double Tokens;
            public double LastRefill;
            public int Strikes;
            public double StrikeWindowStart;
        }

        private readonly double capacity;
        private readonly double refillPerSecond;
        private readonly int maxStrikes;
        private readonly double strikeWindowSeconds;
        private readonly Dictionary<ulong, Bucket> buckets = new Dictionary<ulong, Bucket>();

        public CoreRateLimiter(double capacity, double refillPerSecond, int maxStrikes, double strikeWindowSeconds)
        {
            this.capacity = capacity;
            this.refillPerSecond = refillPerSecond;
            this.maxStrikes = maxStrikes;
            this.strikeWindowSeconds = strikeWindowSeconds;
        }

        public RateLimitResult Check(ulong key, double nowSeconds)
        {
            if (!buckets.TryGetValue(key, out Bucket bucket))
            {
                bucket = new Bucket { Tokens = capacity, LastRefill = nowSeconds, StrikeWindowStart = nowSeconds };
                buckets[key] = bucket;
            }

            double elapsed = nowSeconds - bucket.LastRefill;
            if (elapsed > 0)
            {
                bucket.Tokens = System.Math.Min(capacity, bucket.Tokens + elapsed * refillPerSecond);
                bucket.LastRefill = nowSeconds;
            }

            if (bucket.Tokens >= 1.0)
            {
                bucket.Tokens -= 1.0;
                return RateLimitResult.Allowed;
            }

            if (nowSeconds - bucket.StrikeWindowStart > strikeWindowSeconds)
            {
                bucket.Strikes = 0;
                bucket.StrikeWindowStart = nowSeconds;
            }

            bucket.Strikes++;
            return bucket.Strikes >= maxStrikes ? RateLimitResult.Kick : RateLimitResult.Dropped;
        }

        public void Forget(ulong key) => buckets.Remove(key);

        public void Clear() => buckets.Clear();
    }
}
