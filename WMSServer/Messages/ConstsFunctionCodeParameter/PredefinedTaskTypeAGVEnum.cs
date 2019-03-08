using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WMSServer
{
    public enum PredefinedTaskTypeAGVEnum
    {
        Move,
        ReleaseTray,
        LoadTray
    }
    public enum PredefinedTaskFirstParameterAGVEnum
    {
        Null,
        MovetoOriginalPoint,
        MovetoInputPoint,
        MovetoOutputPoint,
        MovetoDockingPlatform
    }
    /// <summary>
    /// TaskType
    /// </summary>
    public class PredefinedTaskTypeAGVHexString
    {
        public const string Move = "0001";
        public const string ReleaseTray = "0002";
        public const string LoadTray = "0003";
    }
    /// <summary>
    /// Parameter1
    /// </summary>
    public class PredefinedTaskFirstParameterAGVHexString
    {
        public const string Null = "0000";
        public const string MovetoOriginalPoint = "000A";
        public const string MovetoInputPoint = "0001";
        public const string MovetoOutputPoint = "0002";
        public const string MovetoPlatform = "0003";
    }
}
