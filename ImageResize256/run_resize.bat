@echo off
cd /d "%~dp0"
py resize_to_256.py
if errorlevel 1 (
    echo.
    echo Python execution failed.
    echo Please make sure Python and Pillow are installed.
    echo Install Pillow with:
    echo py -m pip install pillow
    pause
)
