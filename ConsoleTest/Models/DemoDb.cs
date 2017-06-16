using Postulate.Orm;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchemaMergeTests.Models
{
    public class DemoDb : SqlServerDb<int>
    {
        public DemoDb() : base("consoleDemo", Environment.UserName)
        {
        }

        public DemoDb(Configuration config) : base(config, "consoleDemo")
        {
        }
    }
}
