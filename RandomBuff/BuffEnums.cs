﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomBuff
{
    public static class BuffEnums
    {
        public static class ProcessID
        {
            public static ProcessManager.ProcessID BuffGameMenu = new("BuffGameMenu");
            public static ProcessManager.ProcessID BuffGameWinScreen = new ProcessManager.ProcessID("BuffGameWinScreen", true);
            public static ProcessManager.ProcessID Cardpedia = new ProcessManager.ProcessID("Cardpedia", true);
            public static ProcessManager.ProcessID CreditID = new ProcessManager.ProcessID("RandomBufdCredit", true);
        }
    }
}
