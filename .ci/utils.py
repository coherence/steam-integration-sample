import argparse
import filecmp
import os
import platform
import subprocess
import sys

def set_binaries_permissions(runtime_dir: str):
    if os.name == 'nt':
        return

    linux_rs = f"{runtime_dir}/linux/replication-server"
    os.chmod(linux_rs, 0o755)
    print(f"Permissions for {linux_rs}: {oct(os.stat(linux_rs).st_mode)}")

    darwin_rs = f"{runtime_dir}/darwin/replication-server"
    os.chmod(darwin_rs, 0o755)
    print(f"Permissions for {darwin_rs}: {oct(os.stat(darwin_rs).st_mode)}")


def docker_build_steam_sample(args):
    docker_args = f"docker build -t {args.tag} -f {args.dockerfile}"
    if args.unity_version:
        docker_args += f" --build-arg UNITY_VERSION={args.unity_version}"
    if args.unity_image_base:
        docker_args += f" --build-arg BASE={args.unity_image_base}"
    docker_args += " ."

    proc = subprocess.Popen(docker_args, shell=True, bufsize=1)
    proc.communicate()

    if proc.returncode != 0:
        print(f"Docker build failed. Command: {docker_args}", file=sys.stderr)

    sys.exit(proc.returncode)


def ensure_volume_path(volume: str):
    mount_point = volume.split(':')[1] if os.name == 'nt' else volume.split(':')[0]
    if not os.path.exists(mount_point):
        os.makedirs(mount_point)


def docker_run_steam_sample(args):
    docker_args = "docker run"
    if args.rm:
        docker_args += " --rm"
    if args.workdir:
        docker_args += f" -w {args.workdir}"
    if args.volumes:
        for volume in args.volumes:
            ensure_volume_path(volume)
            docker_args += f" -v {volume}"  
    if args.additional_volumes:
        for volume in args.additional_volumes:
            ensure_volume_path(volume)
            docker_args += f" -v {volume}"
    if args.entrypoint:
        docker_args += f" --entrypoint {args.entrypoint}"
    docker_args += f" {args.tag}"

    print(f"Running docker command (editor args omitted): {docker_args}")
    docker_args += f" {' '.join(args.editor_args)}"

    proc = subprocess.Popen(docker_args, shell=True, bufsize=1)
    proc.communicate()
    sys.exit(proc.returncode)


def to_lowercase(value: str):
    return value.lower()


def str_to_bool(value: str):
    value = value.lower()
    if value in ['true', '1', 't', 'y', 'yes']:
        return True
    elif value in ['false', '0', 'f', 'n', 'no']:
        return False
    else:
        raise ValueError(f"Invalid boolean value: {value}")


def get_default_workdir():
    return "/"


def common_docker_run_args(parser):
    parser.add_argument('-r', '--rm', help='Remove container after run', default=True, type=str_to_bool)
    parser.add_argument('--additional-volumes',
                                   help='Additional volumes to mount on top of the default ones', nargs='+')
    parser.add_argument('-w', '--workdir', help='Working directory', default=get_default_workdir())
    parser.add_argument('-e', '--entrypoint', help='Custom entrypoint', default=None)
    parser.add_argument('-a', '--editor-args', nargs='+', help='Arguments to pass to the test run')


def main():
    parser = argparse.ArgumentParser(description='Various utilities',
                                     formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    command = parser.add_subparsers(required=True, dest="command")

    parser.add_argument('-p', '--project-path', help='Relative path to the Unity project',
                        default="steam-integration-sample")

    # Set permissions command
    bin_perm_parser = command.add_parser("set-binaries-permissions",
                                         help="Set permissions for all binaries SDK binaries")
    bin_perm_parser.add_argument('-r', '--runtime-dir', help='Relative path to the runtime directory',
                                 default="sdk/.Runtime")

    def get_default_volumes():
        container_dir = "/TestResults"
        return [f'{os.path.join(os.getcwd(), "TestResults")}:{container_dir}']

    # Docker build command
    docker_build_parser = command.add_parser("docker-build-steam-sample", help="Build docker image for in-editor Steam Sample project")
    docker_build_parser.add_argument('-u', '--unity-version', help='Unity version')
    docker_build_parser.add_argument('-b', '--unity-image-base', help='Unity image base')
    docker_build_parser.add_argument('-t', '--tag', help='Docker image tag', default="steam")
    docker_build_parser.add_argument('-f', '--dockerfile', help='Dockerfile path',
                                     default=f".ci/Dockerfile")

    # Docker run command
    docker_run_parser = command.add_parser("docker-run-steam-sample", help="Run docker image for Steam Sample project")
    docker_run_parser.add_argument('-t', '--tag', help='Docker image tag', default="steam")
    docker_run_parser.add_argument('-v', '--volumes', help='Volumes to mount. Overrides default volumes',
                                   default=get_default_volumes(), nargs='+')
    common_docker_run_args(docker_run_parser)

    args = parser.parse_args()

    print(f'Running command: {args.command}')

    if args.command == "set-binaries-permissions":
        set_binaries_permissions(args.runtime_dir)
    elif args.command == "docker-build-steam-sample":
        docker_build_steam_sample(args)
    elif args.command == "docker-run-steam-sample":
        docker_run_steam_sample(args)

if __name__ == "__main__":
    main()
