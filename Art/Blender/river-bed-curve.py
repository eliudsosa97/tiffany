import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import run
from river_kit import build_bank as build


if __name__ == "__main__":
    run(__file__, build)
