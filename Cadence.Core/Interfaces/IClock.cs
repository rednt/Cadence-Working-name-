using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cadence.Core.Interfaces
{
    public interface IClock
    {
        DateTime now { get; }
    }
}