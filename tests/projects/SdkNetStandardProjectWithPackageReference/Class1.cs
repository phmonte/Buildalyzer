using System;
using NodaTime;

namespace SdkNetStandardProjectWithPackageReference
{
    public class Class1
    {
        public void Foo()
        {
            Instant now = SystemClock.Instance.GetCurrentInstant();
        }
    }
}
