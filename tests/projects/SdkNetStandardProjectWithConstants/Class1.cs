using System;

namespace SdkNetStandardProject
{
#if DEF1
    public class Class1
    {
        public void Foo()
        {
            Console.WriteLine("Bar");
        }
    }
#endif
    
#if DEF2
    public class Class2
    {
        public void Foo()
        {
            Console.WriteLine("Bar");
        }
    }
#endif
}
