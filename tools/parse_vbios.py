# 解析 AMD 显卡 VBIOS 中的 PowerPlay 表（只读，不需要驱动）。
# 依赖: pip install upp
# 用法: python parse_vbios.py <vbios.rom> [out_pp.bin]
import io, contextlib, pathlib, sys
from upp import decode as D

def main():
    rom = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else "pp_table.bin"
    D.extract_rom(rom, out)                 # 从 VBIOS 中提取 PowerPlay 表
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        D.dump_pp_table(out)                # 解码为可读结构
    lines = buf.getvalue().splitlines()
    want = ("FreqTableGfx", "GfxclkFmax", "DefaultMaxVoltage", "BoostMaxVoltage", "VftFMin", "qVft")
    for i, l in enumerate(lines):
        if any(l.strip().startswith(k) for k in want):
            print(l.strip())
            for j in range(i + 1, min(i + 4, len(lines))):
                if lines[j].strip():
                    print("   ", lines[j].strip())

if __name__ == "__main__":
    main()
