using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Buildalyzer
{
    public class ProjectItem
    {
        public string ItemSpec { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        internal ProjectItem(ITaskItem taskItem)
        {
            ItemSpec = taskItem.ItemSpec;
            Metadata = taskItem.MetadataNames.Cast<string>().ToDictionary(x => x, x => taskItem.GetMetadata(x));
        }
    }
}