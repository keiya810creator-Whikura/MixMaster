@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

if not exist "InputSprites" mkdir "InputSprites"
if not exist "Output512" mkdir "Output512"

where py >nul 2>nul
if %errorlevel%==0 (
    set "PYTHON=py"
    goto :CHECK
)

where python >nul 2>nul
if %errorlevel%==0 (
    set "PYTHON=python"
    goto :CHECK
)

echo Python 3 が見つかりません。
pause
exit /b 1

:CHECK
%PYTHON% -c "import PIL, numpy, cv2" >nul 2>nul
if not %errorlevel%==0 (
    echo 必要ライブラリをインストールしています...
    %PYTHON% -m pip install --user Pillow numpy opencv-python-headless
    if not %errorlevel%==0 (
        echo インストールに失敗しました。
        pause
        exit /b 1
    )
)

echo.
%PYTHON% "%~dp0AutoSplitSprites512_v3.py"
echo.
pause
endlocal
