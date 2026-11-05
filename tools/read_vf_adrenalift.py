# 通过 Adrenalift 的引擎读取真实 V/F 数据（需要其依赖的内核驱动已安装）。
# 用法: python read_vf_adrenalift.py <Adrenalift 源码目录> <输出 json>
# 说明: Adrenalift 为 GPL-3.0，RadeonTune 同为 GPL-3.0；本脚本只调用其公开函数。
import json, sys, pathlib

def main():
    src = pathlib.Path(sys.argv[1]).resolve()
    out = pathlib.Path(sys.argv[2]).resolve()
    sys.path.insert(0, str(src))
    sys.path.insert(0, str(src / "engine"))
    from engine.overclock_engine import OverclockEngine  # type: ignore

    eng = OverclockEngine() if callable(OverclockEngine) else None
    data = {}
    for fn in ("read_od", "read_pptable_at_addr", "read_smu_table_raw", "get_dpm_ranges"):
        f = getattr(eng, fn, None) if eng else None
        if not callable(f):
            continue
        try:
            data[fn] = f()
        except Exception as e:
            data[fn] = {"error": f"{type(e).__name__}: {e}"}
    out.write_text(json.dumps(data, default=str, ensure_ascii=False, indent=2), encoding="utf-8")
    print("written", out)

if __name__ == "__main__":
    main()
