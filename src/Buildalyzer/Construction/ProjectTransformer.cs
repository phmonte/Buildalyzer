using System.Linq;
using System.Xml.Linq;

namespace Buildalyzer.Construction
{
    public interface IProjectTransformer
    {
        void Transform(XDocument projectDocument);

    }
}