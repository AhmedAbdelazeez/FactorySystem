using System;
using System.Threading.Tasks;

namespace Test_DATA
{
    public static class TestCycleRunner
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            await ComprehensiveTestDataSeeder.SeedAndRunFullTestAsync(services);
        }
    }
}
