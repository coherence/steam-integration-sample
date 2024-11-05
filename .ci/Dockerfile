ARG UNITY_VERSION=2021.3.27f1
ARG BASE=1

FROM unityci/editor:$UNITY_VERSION-base-$BASE

# Add Microsoft package repo. Only do this if version is 18.04 or 20.04
RUN /bin/bash -c '. /etc/os-release; \
    if [[ ${VERSION_ID} == "18.04" || ${VERSION_ID} == "20.04" ]]; then \
        wget https://packages.microsoft.com/config/${ID}/${VERSION_ID}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb; \
        dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb; \
    else \
        echo "Not adding Microsoft package repo"; \
    fi'

# Install packages
RUN apt-get update 
RUN apt-get install -y python3
RUN apt-get install -y dotnet-sdk-6.0

COPY *.ulf ./
COPY sdk sdk
COPY steam-integration-sample steam-integration-sample

WORKDIR .

# -u flag is needed to avoid buffering output
ENTRYPOINT [ "python3", "-u", ".ci/run_tests.py" ]
