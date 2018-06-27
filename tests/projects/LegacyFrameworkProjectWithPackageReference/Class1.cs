using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace LegacyFrameworkProjectWithPackageReference
{
    public class Class1
    {
        public void Foo()
        {
            Instant now = SystemClock.Instance.GetCurrentInstant();
        }
    }
}
