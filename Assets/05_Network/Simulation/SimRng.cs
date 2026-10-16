namespace StarPixelSurge.Network.Simulation
{
    /// <summary>
    /// 确定性伪随机数（xorshift64*）。仿真用它注入可控的延迟抖动与丢包，
    /// 相同种子必然产生相同结果，便于复现与断言。
    /// </summary>
    public struct SimRng
    {
        private ulong _state;

        public SimRng(ulong seed)
        {
            _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextUInt64()
        {
            var x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>[0,1) 均匀分布。</summary>
        public float NextFloat()
        {
            return (NextUInt64() >> 40) / (float)(1UL << 24);
        }

        /// <summary>[min,max) 均匀分布。</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }
    }
}
