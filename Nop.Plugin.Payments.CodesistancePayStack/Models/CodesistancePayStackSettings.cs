using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.CodesistancePayStack.Models
{
    public class CodesistancePayStackSettings: ISettings
    {
        public string PayStackApiKey { get; set; }
    }
}
