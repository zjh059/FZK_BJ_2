"""Windows launcher for the bundled DOCX renderer.

The bundled renderer expects ``soffice`` and Poppler on PATH.  On this
workstation LibreOffice is installed, but its console executable is named
``soffice.com`` and Poppler lives in the bundled runtime directory.  This
small launcher only supplies those two concrete Windows paths; the actual
rendering and page naming still come from the official ``render_docx.py``.
"""

from __future__ import annotations

import importlib.util
import subprocess
import sys
from pathlib import Path


RENDERER = Path(
    r"C:\Users\ZJH\.codex\plugins\cache\openai-primary-runtime\documents"
    r"\26.630.12135\skills\documents\render_docx.py"
)
SOFFICE = r"C:\Program Files\LibreOffice\program\soffice.com"
POPPLER = (
    r"C:\Users\ZJH\.cache\codex-runtimes\codex-primary-runtime"
    r"\dependencies\native\poppler\Library\bin"
)


spec = importlib.util.spec_from_file_location("bundled_render_docx", RENDERER)
if spec is None or spec.loader is None:
    raise RuntimeError(f"无法加载渲染器：{RENDERER}")

renderer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(renderer)


# LibreOffice 的 GUI 启动器 soffice.exe 会立即返回；console 版本会等待转换完成。
_subprocess_run = subprocess.run


def _run_with_windows_soffice(args, *pargs, **kwargs):
    if isinstance(args, (list, tuple)) and args and args[0] == "soffice":
        args = [SOFFICE, *args[1:]]

        # 官方脚本按类 Unix 路径拼接 ``file://``。Windows 下需要把
        # ``C:\\...`` 转成标准的 ``file:///C:/...`` URI，否则 LibreOffice
        # 会等待一个无效的用户配置目录。
        prefix = "-env:UserInstallation=file://"
        for index, value in enumerate(args):
            if isinstance(value, str) and value.startswith(prefix):
                profile_path = value[len(prefix) :]
                args[index] = "-env:UserInstallation=" + Path(profile_path).as_uri()
    return _subprocess_run(args, *pargs, **kwargs)


renderer.subprocess.run = _run_with_windows_soffice


# 明确告诉 pdf2image 使用工作区依赖中自带的 Poppler。
_convert_from_path = renderer.convert_from_path
_pdfinfo_from_path = renderer.pdfinfo_from_path


def _convert_pdf(*args, **kwargs):
    kwargs.setdefault("poppler_path", POPPLER)
    return _convert_from_path(*args, **kwargs)


def _read_pdf_info(*args, **kwargs):
    kwargs.setdefault("poppler_path", POPPLER)
    return _pdfinfo_from_path(*args, **kwargs)


renderer.convert_from_path = _convert_pdf
renderer.pdfinfo_from_path = _read_pdf_info


if __name__ == "__main__":
    renderer.main()
