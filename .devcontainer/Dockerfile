FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

# Base image ships a stale Yarn apt source (dl.yarnpkg.com) whose GPG key was
# rotated upstream; any apt-get update (e.g. the common-utils feature) fails with
# NO_PUBKEY. Drop the broken source — the node feature reinstalls yarn cleanly.
RUN rm -f /etc/apt/sources.list.d/yarn.list /usr/share/keyrings/yarnkey.gpg; \
    sed -i '\|dl.yarnpkg.com|d' /etc/apt/sources.list 2>/dev/null || true
