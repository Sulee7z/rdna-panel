using System;
using System.Collections.Generic;
using System.Linq;

namespace RadeonTune
{
    public class EdidTiming
    {
        public int PixelClockKHz;
        public int HActive, HBlank, VActive, VBlank;
        public int HFront, HSync, VFront, VSync;
        public bool HNegative, VNegative;
        public int HTotal => HActive + HBlank;
        public int VTotal => VActive + VBlank;
        public double Refresh => HTotal * VTotal > 0 ? PixelClockKHz * 1000.0 / (HTotal * (double)VTotal) : 0;
    }

    /// <summary>EDID 解析：显示器名、范围限制、首选时序；用于生成合法的自定义分辨率参数。</summary>
    public class EdidInfo
    {
        public string MonitorName = "";
        public int MinVHz, MaxVHz, MaxPixelClockMHz;
        public List<EdidTiming> Timings = new();

        public static EdidInfo Parse(string hex)
        {
            var info = new EdidInfo();
            if (string.IsNullOrEmpty(hex) || hex.Length < 256) return info;
            var raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++) raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

            for (int i = 0; i < 4; i++)
            {
                int off = 54 + i * 18;
                if (off + 18 > raw.Length) break;
                if (raw[off] == 0 && raw[off + 1] == 0)
                {
                    if (raw[off + 3] == 0xFC)
                    {
                        var name = raw.Skip(off + 5).Take(13).TakeWhile(b => b != 0x0A && b != 0).ToArray();
                        info.MonitorName = System.Text.Encoding.ASCII.GetString(name).Trim();
                    }
                    else if (raw[off + 3] == 0xFD)
                    {
                        info.MinVHz = raw[off + 5];
                        info.MaxVHz = raw[off + 6];
                        info.MaxPixelClockMHz = raw[off + 9] * 10;
                    }
                    continue;
                }
                var t = new EdidTiming
                {
                    PixelClockKHz = (raw[off] | raw[off + 1] << 8) * 10,
                    HActive = raw[off + 2] | (raw[off + 4] & 0xF0) << 4,
                    HBlank = raw[off + 3] | (raw[off + 4] & 0x0F) << 8,
                    VActive = raw[off + 5] | (raw[off + 7] & 0xF0) << 4,
                    VBlank = raw[off + 6] | (raw[off + 7] & 0x0F) << 8,
                    HFront = raw[off + 8] | (raw[off + 11] & 0xC0) << 2,
                    HSync = raw[off + 9] | (raw[off + 11] & 0x30) << 4,
                    VFront = raw[off + 10] >> 4 | (raw[off + 11] & 0x0C) << 2,
                    VSync = (raw[off + 10] & 0x0F) | (raw[off + 11] & 0x03) << 4,
                    HNegative = (raw[off + 17] & 0x02) != 0,
                    VNegative = (raw[off + 17] & 0x04) != 0,
                };
                info.Timings.Add(t);
            }
            return info;
        }

        /// <summary>
        /// 按 ADLX 时序标准生成时序：0=CVT, 1=CVT-RB, 2=GTF, 3=DMT, 4=手动（沿用当前模式消隐）。
        /// </summary>
        public static EdidTiming Compute(int w, int h, int hz, int standard, EdidTiming template = null)
        {
            switch (standard)
            {
                case 0:  // CVT：按比例消隐
                    {
                        int hBlank = Math.Max(160, (int)Math.Round(w * 0.075));
                        int vBlank = Math.Max(20, (int)Math.Round(h * 0.04));
                        int hTotal = w + hBlank, vTotal = h + vBlank;
                        return new EdidTiming
                        {
                            HActive = w, HBlank = hBlank, VActive = h, VBlank = vBlank,
                            HFront = 48, HSync = 32, VFront = 3, VSync = 5,
                            PixelClockKHz = (int)Math.Round(hTotal * (double)vTotal * hz / 1000.0),
                            HNegative = true, VNegative = true,
                        };
                    }
                case 2:  // GTF：较大消隐
                    {
                        int hTotal = (int)Math.Round(w * 1.33);
                        int vTotal = h + Math.Max(20, (int)Math.Round(h * 0.05));
                        return new EdidTiming
                        {
                            HActive = w, HBlank = hTotal - w, VActive = h, VBlank = vTotal - h,
                            HFront = 48, HSync = 32, VFront = 3, VSync = 5,
                            PixelClockKHz = (int)Math.Round(hTotal * (double)vTotal * hz / 1000.0),
                            HNegative = false, VNegative = false,
                        };
                    }
                case 3:  // DMT：VESA DMT 典型消隐
                    {
                        int hBlank = Math.Max(160, (int)Math.Round(w * 0.11));
                        int vBlank = Math.Max(20, (int)Math.Round(h * 0.033));
                        int hTotal = w + hBlank, vTotal = h + vBlank;
                        return new EdidTiming
                        {
                            HActive = w, HBlank = hBlank, VActive = h, VBlank = vBlank,
                            HFront = 48, HSync = 32, VFront = 3, VSync = 5,
                            PixelClockKHz = (int)Math.Round(hTotal * (double)vTotal * hz / 1000.0),
                            HNegative = false, VNegative = true,
                        };
                    }
                case 4 when template != null:  // 手动：沿用当前模式的消隐比例
                    {
                        double hRatio = template.HTotal / (double)Math.Max(1, template.HActive);
                        double vRatio = template.VTotal / (double)Math.Max(1, template.VActive);
                        int hTotal = (int)Math.Round(w * hRatio);
                        int vTotal = (int)Math.Round(h * vRatio);
                        return new EdidTiming
                        {
                            HActive = w, HBlank = Math.Max(1, hTotal - w), VActive = h, VBlank = Math.Max(1, vTotal - h),
                            HFront = template.HFront, HSync = template.HSync,
                            VFront = template.VFront, VSync = template.VSync,
                            PixelClockKHz = (int)Math.Round(hTotal * (double)vTotal * hz / 1000.0),
                            HNegative = template.HNegative, VNegative = template.VNegative,
                        };
                    }
                default:  // CVT-RB
                    return ComputeCvtRb(w, h, hz);
            }
        }

        /// <summary>按 CVT-RB 规则生成目标分辨率的时序（48/32 前沿同步，160 水平消隐）。</summary>
        public static EdidTiming ComputeCvtRb(int w, int h, int hz)
        {
            int hTotal = w + 160;
            int vBlank = Math.Max(20, (int)Math.Ceiling(460.0 / hz));
            int vTotal = h + vBlank;
            return new EdidTiming
            {
                HActive = w, HBlank = 160, VActive = h, VBlank = vTotal - h,
                HFront = 48, HSync = 32, VFront = 3, VSync = 5,
                PixelClockKHz = (int)Math.Round(hTotal * (double)vTotal * hz / 1000.0),
                HNegative = false, VNegative = false,
            };
        }

        /// <summary>
        /// 校验目标参数。刷新率上限优先取「Windows 实际枚举到的该分辨率最高刷新率」，
        /// 因为高刷显示器的 EDID range 描述符常常只写保守值（例如 2K320 显示器写 48–65），
        /// 仅凭 EDID 会把合法的高刷模式误判为越界。
        /// </summary>
        public List<string> Validate(int w, int h, int hz, EdidTiming t, int modeMaxHz = 0)
        {
            var problems = new List<string>();
            int maxHz = Math.Max(MaxVHz, modeMaxHz);
            if (maxHz > 0 && hz > maxHz + 1) problems.Add($"刷新率 {hz} Hz 超过该分辨率上限 {maxHz} Hz");
            if (MinVHz > 0 && hz < MinVHz - 1) problems.Add($"刷新率 {hz} Hz 低于下限 {MinVHz} Hz");
            if (MaxPixelClockMHz > 0 && t.PixelClockKHz / 1000.0 > MaxPixelClockMHz + 1)
                problems.Add($"像素时钟 {t.PixelClockKHz / 1000.0:F1} MHz 超过 EDID 上限 {MaxPixelClockMHz} MHz");
            if (t.PixelClockKHz <= 0) problems.Add("像素时钟计算异常");
            return problems;
        }
    }
}
