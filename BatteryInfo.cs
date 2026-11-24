using System;

namespace WinPods
{
    public class BatteryInfo
    {
        // -1 means unknown.
        public int Left = -1, Right = -1, Case = -1;
        public bool LeftCharging, RightCharging, CaseCharging;

        /// <summary>
        /// Parses Apple's "proximity pairing" BLE advertisement (company 0x004C, type 0x07).
        /// data = manufacturer payload without the 2-byte company id.
        /// </summary>
        public static bool TryParse(byte[] d, BatteryInfo into)
        {
            if (d == null || d.Length < 8 || d[0] != 0x07) return false;

            int status = d[5];
            int pods = d[6];
            int flags = d[7] >> 4;
            int caseNibble = d[7] & 0x0F;

            // Bit 1 of the status high nibble tells which pod is "primary" (left or right).
            bool flipped = ((status >> 4) & 0x02) == 0;

            int a = pods >> 4, b = pods & 0x0F;     // a: first pod, b: second pod
            int left = flipped ? a : b, right = flipped ? b : a;

            // Charging bits: 0x01 = one pod, 0x02 = other pod, 0x04 = case.
            bool chA = (flags & 0x01) != 0, chB = (flags & 0x02) != 0;
            bool leftCh = flipped ? chB : chA, rightCh = flipped ? chA : chB;

            if (left <= 10) { into.Left = left * 10; into.LeftCharging = leftCh; }
            if (right <= 10) { into.Right = right * 10; into.RightCharging = rightCh; }
            if (caseNibble <= 10) { into.Case = caseNibble * 10; into.CaseCharging = (flags & 0x04) != 0; }
            return true;
        }

        public string Summary()
        {
            string F(int v) => v < 0 ? "--" : v + "%";
            return "L " + F(Left) + "  R " + F(Right) + "  Case " + F(Case);
        }
    }
}
