namespace SharedKernel.MathUtil
{
    public class LogNormal
    {
        private readonly Random random = new Random();
        private readonly double mu;
        private readonly double sigma;
        public LogNormal(double mu, double sigma)
        {
            this.mu = Math.Log(mu) - sigma * sigma / 2;
            this.sigma = Math.Log(1 + sigma * sigma / (mu * mu));
        }
        private double NextNormal()
        {
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public double Next()
        {
            double normal = NextNormal();
            return Math.Exp(mu + sigma * normal);
        }
    }
}