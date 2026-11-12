"""Create (or refresh) the asset factory's private Python environment in .venv.

Run once per machine, and again whenever requirements.txt changes:
    python src/asset-factory/setup_env.py
Then run the tools with the environment's Python:
    src/asset-factory/.venv/Scripts/python   (Windows)
    src/asset-factory/.venv/bin/python       (macOS, Linux)
"""
import os
import subprocess
import sys
import venv

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.join(HERE, ".venv")

if not os.path.isdir(ENV):
    venv.create(ENV, with_pip=True)
py = os.path.join(ENV, "Scripts", "python.exe") if os.name == "nt" else os.path.join(ENV, "bin", "python")
subprocess.check_call([py, "-m", "pip", "install", "--disable-pip-version-check", "-q", "-r", os.path.join(HERE, "requirements.txt")])
print("ready:", py)
