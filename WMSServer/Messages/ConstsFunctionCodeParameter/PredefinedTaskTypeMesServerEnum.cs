using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum PredefinedTaskTypeMesServerEnum
    {
        CarryTraytoDockingPlatform,
        ReleaseTray,
        CarryEmptyTraytoWarehouse
    }
    public enum PredefinedTaskFirstParameterMesServerEnum
    {
        Bottle,
        Lid
    }
    public class PredefinedTaskTypeMesServerHexString
    {
        public const string CarryTraytoDockingPlatform = "0001";
        public const string ReleaseTray = "0002";
        public const string CarryEmptyTraytoWarehouse = "0003";
    }
    public class PredefinedTaskFirstParameterMesServerHexString
    {
        public const string Bottle = "0101";
        public const string Lid = "0201";
    }

}
