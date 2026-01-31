import sys
import os

# Add src to sys.path so that battle_cat_calc can be imported during tests
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '../src')))
