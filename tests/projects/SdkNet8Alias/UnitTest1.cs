extern alias Alias;
extern alias OtherAlias;

using Alias::Xunit;
using OtherAlias::SdkNet8CS12FeaturesProject;

namespace SdkNet8Alias
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var t = new Class1();
        }
    }
}
