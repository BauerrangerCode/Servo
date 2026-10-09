"""Builds the Servo packages for Windows, macOS and Linux into artifacts/.

Run from the repository root:  python scripts/publish_servo.py [--only win-x64 linux-x64 osx-arm64 osx-x64]

Each package holds the Servo launcher (one self-contained program) next to the published app (the 'wwwroot'
folder). The packages contain no rules data; it is loaded with "Update data" on first start.
Needs the .NET SDK and, to build for other systems, internet access for the runtime packs. Python 3 standard library only.
"""
import argparse
import shutil
import subprocess
import tarfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"
APP_PROJECT = ROOT / "src" / "Servitor.App" / "Servitor.App.csproj"
LAUNCHER_PROJECT = ROOT / "src" / "Servitor.Launcher" / "Servitor.Launcher.csproj"
ICON_DIRECTORY = ROOT / "src" / "Servitor.Launcher" / "Assets"

TARGETS = ["win-x64", "linux-x64", "osx-arm64", "osx-x64"]

INFO_PLIST = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Servo</string>
  <key>CFBundleDisplayName</key><string>Servo</string>
  <key>CFBundleIdentifier</key><string>app.servo.launcher</string>
  <key>CFBundleExecutable</key><string>Servo</string>
  <key>CFBundleIconFile</key><string>servo</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>0.1</string>
  <key>CFBundleShortVersionString</key><string>0.1</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
"""


def run(*command):
    print("+", " ".join(str(part) for part in command), flush=True)
    subprocess.run([str(part) for part in command], check=True, cwd=ROOT)


def publish_app(app_directory):
    run("dotnet", "publish", APP_PROJECT, "-c", "Release", "-p:ExcludeRulesData=true", "-o", app_directory)
    web_root = app_directory / "wwwroot"
    # The launcher does not serve pre-compressed files, so they would only add size.
    for compressed in [*web_root.rglob("*.br"), *web_root.rglob("*.gz")]:
        compressed.unlink()
    return web_root


def publish_launcher(target, output):
    run(
        "dotnet", "publish", LAUNCHER_PROJECT, "-c", "Release", "-r", target, "--self-contained",
        "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true", "-o", output,
    )


def add_directory_to_zip(archive, directory, prefix):
    for path in sorted(directory.rglob("*")):
        relative = Path(prefix) / path.relative_to(directory)
        if path.is_dir():
            continue
        info = zipfile.ZipInfo.from_file(path, relative.as_posix())
        info.compress_type = zipfile.ZIP_DEFLATED
        # Keep the executable bit for the launcher; Finder and unzip restore it.
        info.external_attr = (0o755 if path.name == "Servo" else 0o644) << 16
        with open(path, "rb") as source:
            archive.writestr(info, source.read())


def package_windows(launcher_output, web_root, destination):
    folder = destination / "Servo-win-x64"
    folder.mkdir(parents=True)
    shutil.copy2(launcher_output / "Servo.exe", folder / "Servo.exe")
    shutil.copytree(web_root, folder / "wwwroot")
    archive_path = ARTIFACTS / "Servo-win-x64.zip"
    with zipfile.ZipFile(archive_path, "w") as archive:
        add_directory_to_zip(archive, folder, "Servo")
    return archive_path


def package_linux(launcher_output, web_root, destination):
    folder = destination / "Servo-linux-x64"
    folder.mkdir(parents=True)
    shutil.copy2(launcher_output / "Servo", folder / "Servo")
    shutil.copy2(ICON_DIRECTORY / "servo.png", folder / "servo.png")
    shutil.copytree(web_root, folder / "wwwroot")

    archive_path = ARTIFACTS / "Servo-linux-x64.tar.gz"

    def with_modes(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ""
        info.mode = 0o755 if info.isdir() or info.name.endswith("/Servo") else 0o644
        return info

    with tarfile.open(archive_path, "w:gz") as archive:
        archive.add(folder, arcname="Servo", filter=with_modes)
    return archive_path


def package_macos(target, launcher_output, web_root, destination):
    bundle = destination / target / "Servo.app"
    macos = bundle / "Contents" / "MacOS"
    resources = bundle / "Contents" / "Resources"
    macos.mkdir(parents=True)
    resources.mkdir(parents=True)
    shutil.copy2(launcher_output / "Servo", macos / "Servo")
    shutil.copytree(web_root, macos / "wwwroot")
    shutil.copy2(ICON_DIRECTORY / "servo.icns", resources / "servo.icns")
    (bundle / "Contents" / "Info.plist").write_text(INFO_PLIST, encoding="utf-8", newline="\n")

    archive_path = ARTIFACTS / f"Servo-{target}.zip"
    with zipfile.ZipFile(archive_path, "w") as archive:
        add_directory_to_zip(archive, bundle, "Servo.app")
    return archive_path


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", nargs="+", choices=TARGETS, default=TARGETS, help="build only these targets")
    arguments = parser.parse_args()

    if ARTIFACTS.exists():
        shutil.rmtree(ARTIFACTS)
    staging = ARTIFACTS / "staging"
    web_root = publish_app(staging / "app")

    results = []
    for target in arguments.only:
        launcher_output = staging / "launcher" / target
        publish_launcher(target, launcher_output)
        if target == "win-x64":
            results.append(package_windows(launcher_output, web_root, staging / "packages"))
        elif target == "linux-x64":
            results.append(package_linux(launcher_output, web_root, staging / "packages"))
        else:
            results.append(package_macos(target, launcher_output, web_root, staging / "packages"))

    shutil.rmtree(staging)
    print()
    for result in results:
        print(f"{result.relative_to(ROOT)}  ({result.stat().st_size / 1_000_000:.0f} MB)")


if __name__ == "__main__":
    main()
