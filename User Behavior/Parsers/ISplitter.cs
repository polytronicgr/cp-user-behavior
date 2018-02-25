﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserBehavior.Parsers
{
    interface ISplitter
    {
        UserBehaviorDatabase TrainingDB { get; }

        UserBehaviorDatabase TestingDB { get; }
    }
}
