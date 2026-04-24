import json
import os
import subprocess
import sys
import zipfile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
MODINFO_PATH = os.path.join(PROJECT_ROOT, "modinfo.json")
CSPROJ_PATH = os.path.join(PROJECT_ROOT, "StoneQuarry.csproj")
ASSETS_DIR = os.path.join(PROJECT_ROOT, "Assets")
BUILD_OUTPUT = os.path.join(PROJECT_ROOT, "bin", "Debug", "net10.0")


def read_version():
    with open(MODINFO_PATH, "r", encoding="utf-8") as f:
        modinfo = json.load(f)
    return modinfo["version"]


def build_project():
    print("Building project...")
    result = subprocess.run(
        ["dotnet", "build", CSPROJ_PATH, "-c", "Debug"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        print("Build FAILED:")
        print(result.stdout)
        print(result.stderr)
        sys.exit(1)
    print("Build succeeded.")


def create_zip(version):
    zip_name = f"StoneQuarryRepacked.{version}.zip"
    zip_dir = os.path.join(PROJECT_ROOT, "zip")
    os.makedirs(zip_dir, exist_ok=True)
    zip_path = os.path.join(zip_dir, zip_name)

    if os.path.exists(zip_path):
        os.remove(zip_path)
        print(f"Deleted existing {zip_name}")

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        # modinfo.json
        zf.write(MODINFO_PATH, "modinfo.json")
        print("  + modinfo.json")

        # modicon.png
        modicon = os.path.join(PROJECT_ROOT, "modicon.png")
        if os.path.exists(modicon):
            zf.write(modicon, "modicon.png")
            print("  + modicon.png")

        # DLL and PDB from build output
        for filename in os.listdir(BUILD_OUTPUT):
            if filename.endswith(".dll") or filename.endswith(".pdb"):
                filepath = os.path.join(BUILD_OUTPUT, filename)
                zf.write(filepath, filename)
                print(f"  + {filename}")

        # Assets directory
        for dirpath, dirnames, filenames in os.walk(ASSETS_DIR):
            for filename in filenames:
                filepath = os.path.join(dirpath, filename)
                arcname = os.path.relpath(filepath, PROJECT_ROOT)
                # Normalize to forward slashes for zip
                arcname = arcname.replace("\\", "/")
                zf.write(filepath, arcname)
                print(f"  + {arcname}")

    print(f"\nCreated {zip_name}")


def main():
    version = read_version()
    print(f"Mod version: {version}")
    build_project()
    create_zip(version)


if __name__ == "__main__":
    main()
