
<p align="center">
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi'><img src='https://mili.one/pics/ArashiAoi1.png' width="50%" height="50%"/></a></p>
<p align="center">
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/README-zhs.md'><img src='https://mili.one/shields/zh-Hans-README.svg' alt='中文简体 Readme' referrerPolicy='no-referrer' /></a>
          <!--- a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/README-zht.md'><img src='https://mili.one/shields/zh-Hant-README.svg' alt='中文繁體/正體 Readme' referrerPolicy='no-referrer' /></a --->
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/README.md'><img src='https://mili.one/shields/English-README.svg' alt='English Readme' referrerPolicy='no-referrer' /></a>
</p>
<p align="center">
          <a href="https://github.com/mili-tan/ArashiDNS.Aoi/actions"><img src="https://img.shields.io/endpoint.svg?url=https%3A%2F%2Factions-badge.atrox.dev%2Fmili-tan%2FArashiDNS.Aoi%2Fbadge&style=flat" alt='GitHub-action' referrerPolicy='no-referrer' /></a>
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/LICENSE.md'><img src='https://img.shields.io/github/license/mili-tan/ArashiDNS.Aoi.svg' alt='license' referrerPolicy='no-referrer' /></a>
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/releases/latest'><img src='https://img.shields.io/github/release/mili-tan/ArashiDNS.Aoi.svg' alt='GitHub-release' referrerPolicy='no-referrer' /></a>
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/releases/latest'><img src='https://img.shields.io/github/downloads/mili-tan/ArashiDNS.Aoi/total.svg' alt='Github All Releases' referrerPolicy='no-referrer' /></a>
          <a href='https://app.fossa.io/projects/git%2Bgithub.com%2Fmili-tan%2FArashiDNS.Aoi?ref=badge_shield'><img src='https://app.fossa.io/api/projects/git%2Bgithub.com%2Fmili-tan%2FArashiDNS.Aoi.svg?type=shield' alt='FOSSA Status' referrerPolicy='no-referrer' /></a>
</p>

----------

## QuickStart

Host your DNS over HTTPS Server just by running `./Arashi.Aoi --upstream 127.0.0.1`. 

If you get `Permission denied`, run `chmod +x ./Arashi.Aoi` to grant execution permission.

OR using Docker. `docker run milkey/arashidns.aoi` 

It is that easy. Use `--help` / `-?` to discover more parameters and get help information.

## Introduction

### Parameters

| Parameter              | Explanation                                                  | Example                       |
| ---------------------- | ------------------------------------------------------------ | ----------------------------- |
| `-?` / `--help`        | Show help information                                        |                               |
| `-l` / `--listen`      | Set the server listening address and port                    | 127.0.0.1:2020                |
| `-u` / `--upstream`    | Set the upstream origin DNS server IP address                | 8.8.8.8                       |
| `-t` / `--timeout`     | Set timeout for query to the upstream DNS server (ms)        | 500                           |
| `-r` / `--retries`     | Set number of retries for query to upstream DNS server       | 5                             |
| `-p` / `--perfix`      | Set your DNS over HTTPS server query prefix                  | "/dns-query"                  |
| `-c` / `--cache`       | Local query cache settings                                   | `full` / `flexible` / `none`  |
| `--log`                | Console log output settings                                  | `full` / `dns-query` / `none` |
| `--tcp`                | Set enable upstream DNS query using TCP only                 |                               |
| `--noecs`              | Set force disable active EDNS Client Subnet                  |                               |
| `-s` / `--https`       | Set enable HTTPS (Self-Signed Certificate by default, **Not Recommended**) |                 |
| `-pfx` / `--pfxfile`   | Set your pfx certificate file path (with optional password)  |"./cert.pfx"                   |
| `-pass` / `--pfxpass`  | Set your pfx certificate password                            |"*passw0rd* "                  |
| `-pem` / `--pemfile`   | Set your pem certificate file path                           |"./cert.pem"                   |
| `-key` / `--keyfile`   | Set your pem certificate key file path                       |"./cert.key"                   |

### Environment Variables

Usually you only need to set them when running in a container (such as Docker). 

And generally only `ARASHI_VAR`  and `PORT` need to be set.

| Variables                     | Explanation                                                  | Example                    |
| ----------------------------- | ------------------------------------------------------------ | -------------------------- |
| `PORT`                        | Set the server listening port                                | 2020                       |
| `ARASHI_ANY`                  | Set the server listening any address                         | true                       |
| `ARASHI_VAR`                  | Set start-up parameters (see above)                          | `-u 127.0.0.1 -r 3`        |
| `ARASHI_RUNNING_IN_CONTAINER` | Manual setting is required only if the container is not identified | true                 |

### Run in Background

##### Windows

- Just double-click it, or run `./Arashi.Aoi.exe` in Command Prompt or Powershell, and click the Minimize button.
- Use [nssm](https://nssm.cc/) to register ArashiDNS.Aoi as a service. It as a service will restart in the unexpected failure.

##### Linux

- Run `nohup ./Arashi.Aoi --upstream 127.0.0.1 &` or use `screen`. Despite being a dirty approach, it just works.
- Use [supervisor](http://supervisord.org/), [pm2](https://pm2.keymetrics.io/), [monit](https://mmonit.com/monit/), [gosuv](https://github.com/codeskyblue/gosuv), or [systemd](https://systemd.io/) as process daemon and keeping ArashiDNS.Aoi running.

## Protocol Compatibility

### Google DNS over HTTPS Json API

When the `ct` parameter's application is not `dns-message` , and with a valid `name` parameter. ArashiDNS.Aoi provides [Google JSON API for DNS over HTTPS (DoH)](https://developers.google.com/speed/public-dns/docs/doh/json) compatible protocol. Parameters are the same, but `cd` , `do` , `random_padding` are not implemented, they will be ignored.

### IETF RFC-8484 DNS over HTTPS

ArashiDNS.Aoi provides complete [IETF DNS-over-HTTPS (RFC 8484)](https://tools.ietf.org/html/rfc8484) Compatibility. The `GET` request needs to contain valid `dns` parameters.

### Features

##### IPv6 Support 

Full IPv6 support is available, but in many cases IPv4 is still preferred. You may need to force `AAAA` lookups or ipv6 server listening addresses.

##### EDNS-Client-Subnet

EDNS-Client-Subnet is enabled by default. Your upstream origin DNS server also needs to support EDNS-Client-Subnet for it to work. If your server is hosted in ECS or behind CDN, The request need to include `X-Forwarded-For` or `X-Real-IP`. 

If you wish to disable it, please enter EDNS-Client-Subnet IP `0.0.0.0` in your client.

## Feedback

- As a beginner, I seek your kind understanding of the issues in the project.
- If you have bug reports or feature request, please feel free to send issues.
- PRs of new feature implementations or bug fixes are greatly appreciated.
- I am not a native English speaker, so please forgive my typo and grammatical errors. Communication in Chinese is preferred if possible.

## Acknowledgements

<img src='https://i.loli.net/2020/08/03/LWNj2BM6mxuYtRU.png' width="10%" height="10%" align="right"/>

> ReSharper is a really amazing tool that made my development several times more efficient.

Thanks to [JetBrains](https://www.jetbrains.com/?from=AuroraDNS) for providing the [ReSharper](https://www.jetbrains.com/ReSharper/?from=AuroraDNS) open source license for this project.

## Credits 

ArashiDNS was born out of open source softwares and the people who support it.

Check out [Credits](https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/CREDITS.md) for a list of our collaborators and other open source softwares used.

## License

Copyright (c) 2020 Milkey Tan. Code released under the [Mozilla Public License 2.0](https://www.mozilla.org/en-US/MPL/2.0/). 
