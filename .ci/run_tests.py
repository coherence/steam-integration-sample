import argparse
import os
import platform
import re
import shutil
import subprocess
import sys
from threading import Thread


DARWIN = 'Darwin'
LINUX = 'Linux'
WINDOWS = 'Windows'
TEST_RESULTS = 'TestResults'

class Config:
    TIMEOUT_EXIT_CODE = 124
    _default_license_file = ".ci/Unity_v2020.x.ulf"

    def __init__(self, args):
        self.project_path = args.project_path or Config.get_default_project_path(args.command)
        self.unity_license_file = args.unity_license_file or Config._default_license_file
        self.license_file_exists = os.path.isfile(self.unity_license_file)
        self.unity_path = args.unity_path or Config.get_unity_path()
        self.bake_timeout_sec = args.bake_timeout_sec
        self.unity_timeout_sec = args.run_unity_timeout_sec
        self.license_timeout_sec = args.license_timeout_sec

    @staticmethod
    def get_unity_path() -> str:
        if platform.system() == LINUX:
            return 'unity-editor'
        raise NotImplementedError(f'Unsupported OS: {os.name}')

    @staticmethod
    def get_default_project_path(command: str):
        return '.'


def get_common_unity_args() -> str:
    return ('-accept-apiupdate '
            '-batchmode '
            '-logfile - '
            '-nographics')


def read_pipe(pipe):
    with pipe:
        for line in iter(pipe.readline, b''):
            try:
                print(line.decode('utf-8').replace('\r\n', '\n'), end='')
            except Exception as _:
                print(f'{line}')


def terminate_timedout_process(process: subprocess.Popen, name: str):
    process.terminate()

    try:
        process.wait(10)
    except:
        pass

    if process.poll() is not None:
        return

    print(f'{name} still running after terminate. Killing\n', file=sys.stderr)

    for i in range(2):
        process.kill()

        try:
            process.wait(10)
        except:
            pass

        if process.poll() is not None:
            return

    print(f'{name} still running after kill. Giving up\n', file=sys.stderr)


def execute_command_with_timeout(name: str, command: str, timeout_sec: int) -> int:
    print(f'\nRunning {name} (timeout: {timeout_sec}s)\n')

    process = subprocess.Popen(command, shell=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    read_thread = Thread(target=read_pipe, args=[process.stdout])
    read_thread.daemon = True
    read_thread.start()

    rc = 0

    try:
        process.wait(timeout=timeout_sec)
        print("\nProcess finished")
        read_thread.join(10)
        print("Output reading finished")
        rc = process.poll()
    except subprocess.TimeoutExpired:
        print(f'\n\n{name} timed out after {timeout_sec} seconds. Terminating\n', file=sys.stderr)
        terminate_timedout_process(process, name)
        read_thread.join(10)

        rc = Config.TIMEOUT_EXIT_CODE
    except Exception as e:
        print(f'\n\n{name} failed with exception: {e}\n', file=sys.stderr)
        rc = -128
    finally:
        out = sys.stdout if rc == 0 else sys.stderr
        print(f'\n\n{name} finished with return code: {rc}\n', out)
        return rc


def execute_unity_command(name: str, config: Config, command: str, timeout_sec: int, use_common_args=True) -> int:
    if use_common_args:
        command = f'{get_common_unity_args()} {command}'

    unity_command = f'"{config.unity_path}" {command}'
    print("START UNITY with command:\n", unity_command.replace(' -', f'\n{" ":4}-'))
    return execute_command_with_timeout(name, unity_command, timeout_sec)


def activate_unity_license(config: Config) -> int:
    if config.license_file_exists:
        unity_args = f'-manualLicenseFile {config.unity_license_file}'
    else:
        print(f'\nLicense file not found at path {config.unity_license_file}. ')
        return 0

    return execute_unity_command('Unity License Activation',
                                 config,
                                 unity_args,
                                 config.license_timeout_sec)


def deactivate_unity_license(config: Config) -> int:
    if config.license_file_exists:
        return 0

    return execute_unity_command('Unity License Deactivation',
                                 config,
                                 "",
                                 config.license_timeout_sec)


def execute_with_unity_license(config: Config, func: callable) -> int:
    rc = activate_unity_license(config)
    if rc != 0:
        return rc

    try:
        return func()
    finally:
        deactivate_unity_license(config)


def try_copy_test_results(src: str, dst: str):
    print(f"Copying test results from {src} to {dst}")

    try:
        if os.path.isfile(src):
            print(f'Copying {src} to {dst}')
            shutil.copy(src, dst)
            return

        if not os.path.exists(dst):
            os.makedirs(dst)

        for root, _, files in os.walk(src):
            for file in files:
                dst_dir = os.path.join(dst, os.path.relpath(root, src)) if root != src else dst
                os.makedirs(dst_dir, exist_ok=True)
                src_path = os.path.join(root, file)
                dst_path = os.path.join(dst_dir, file)
                print(f'Copying {src_path} to {dst_path}')
                shutil.copy(src_path, dst_path)
    except Exception as e:
        print(f'Failed to copy test results ({src}): {e}')


def run_project(config: Config) -> int:
    def func() -> int:
        command = (f'-projectPath {config.project_path} '
                   '-runTests '
                   '-testResults unit-test-results.xml '
                   '-testPlatform EditMode')

        rc = execute_unity_command("SDK unit tests", config, command, config.unity_timeout_sec)

        if not os.path.isdir(TEST_RESULTS):
            os.makedirs(TEST_RESULTS)
        try_copy_test_results(f'{config.project_path}/unit-test-results.xml', f'{TEST_RESULTS}/unit-test-results.xml')
        return rc

    return execute_with_unity_license(config, func)


def main():

    parser = argparse.ArgumentParser(description='Runs Steam sample project',
                                     formatter_class=argparse.ArgumentDefaultsHelpFormatter)

    commands = ['run-project', 'deactivate-license']
    parser.add_argument('-c', '--command', type=str, required=True,
                        help=f'Command to run: {commands}',
                        choices=commands)
    parser.add_argument('-e', '--unity-path',
                        type=str, help='Path to Unity executable. Default is unity-editor on '
                                       'Linux')
    parser.add_argument('-l', '--unity-license-file',
                        type=str, help='Path to Unity license file')
    parser.add_argument('--project-path',
                        type=str, help='Path to Unity project')
    parser.add_argument('--run-unity-timeout-sec',
                        type=int, help='Timeout in seconds for running Unity', default=600)
    parser.add_argument('--bake-timeout-sec',
                        type=int, help='Timeout in seconds for baking RSL code', default=240)
    parser.add_argument('--license-timeout-sec',
                        type=int, help='Timeout in seconds for activating license', default=60)


    args = parser.parse_args()
    config = Config(args)

    print(f'Running {args.command}')

    if args.command == 'run-project':
        sys.exit(run_project(config))
    elif args.command == 'deactivate-license':
        sys.exit(deactivate_unity_license(config))

    raise NotImplementedError(f'Unsupported command: {args.command}')


if __name__ == '__main__':
    main()