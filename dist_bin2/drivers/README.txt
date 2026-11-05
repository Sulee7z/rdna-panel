此目录用于放置可选的第三方驱动文件（不随本程序分发）。

SMU 直读需要 InpOut32：
  1. 从 https://www.highrez.co.uk/downloads/inpout32/ 下载；
  2. 以管理员身份安装一次；
  3. 把 inpoutx64.dll 复制到本目录；
  4. 重启 RadeonTune，超频页的「SMU 直读（可选依赖）」应显示 ready。

卸载（会影响其他使用该驱动的工具，例如 Adrenalift）：
  sc.exe stop inpoutx64
  sc.exe delete inpoutx64

未放置任何文件时，本程序不会尝试加载或安装驱动。
