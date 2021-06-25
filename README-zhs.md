
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

## 快速开始

搭建你的 DNS over HTTPS 服务器只需要运行 `./Arashi.Aoi --upstream 127.0.0.1`。

如果你得到了 `Permission denied`，请运行 `chmod +x ./Arashi.Aoi` 来给予可执行权限。

或者使用 Docker。`docker run milkey/arashidns.aoi`

就这么简单，使用 `--help` / `-?` 来探索更多参数和获取帮助信息。

## 介绍

### 参数

| 参数                   | 说明                                         | 示例                          |
| ---------------------- | -------------------------------------------- | ----------------------------- |
| `-?` / `--help`        | 显示帮助信息                                 |                               |
| `-l` / `--listen`      | 设置服务器监听地址和端口                     | 127.0.0.1:2020                |
| `-u` / `--upstream`    | 设置上游查询源 DNS 服务器 IP 地址            | 8.8.8.8                       |
| `-t` / `--timeout`     | 设置向上游查询的超时时间（毫秒）             | 500                           |
| `-r` / `--retries`     | 设置向上游查询的重试次数                     | 5                            |
| `-p` / `--perfix`      | 设置 DNS over HTTPS 的查询路径前缀           | "/dns-query"                  |
| `-c` / `--cache`       | 本地缓存设置                                 | `full` / `flexible` / `none`  |
| `--log`                | 控制台日志输出设置                           | `full` / `dns-query` / `none` |
| `--tcp`                | 启用向上游仅TCP查询                          |                               |
| `--noecs`              | 强制停用主动EDNS Client Subnet               |                               |
| `-s` / `--https`       | 启用 HTTPS（默认使用自签名证书，**不推荐**） |                               |
| `--chinalist` | 使用 DNSPod D+ 分流，需要目录中包含 `china_whitelist.list` |                          |
| `-pfx` / `--pfxfile`   | 设置 pfx 证书文件路径（可选密码）。 |"./cert.pfx"      |
| `-pass` / `--pfxpass`   | 设置 pfx 证书密码 |"*passw0rd* "      |
| `-pem` / `--pemfile`   | 设置 pem 证书文件路径 |"./cert.pem"      |
| `-key` / `--keyfile`   | 设置 pem 证书密钥文件路径 |"./cert.key"      |

### 环境变量

通常只有在容器环境（如Docker）中运行时才需要设置它们，而且一般只需要设置 `ARASHI_VAR` 和 `PORT`。

| 变量                          | 说明                                           | 示例                |
| ----------------------------- | ---------------------------------------------- | ------------------- |
| `PORT`                        | 设置服务器监听端口                             | 2020                |
| `ARASHI_ANY`                  | 设置服务器监听任意地址（0.0.0.0）              | true                |
| `ARASHI_VAR`                  | 设置启动参数（见上文）                         | `-u 127.0.0.1 -r 3` |
| `ARASHI_RUNNING_IN_CONTAINER` | 只在没能正确识别容器环境情况下，才需要手动设置 | true                |

### 后台运行

##### Windows

- 只要双击就好，或是在 CMD 或 Powershell 中运行 `./Arashi.Aoi.exe`，然后点击窗口最小化按钮。
- 使用 [nssm](https://nssm.cc/) 将 ArashiDNS.Aoi 注册为服务，作为服务它会在意外崩溃时重启。

##### Linux

- 运行 `nohup ./Arashi.Aoi --upstream 127.0.0.1 &` 或使用 `screen`，这或许可能不是一个很好的主意，但是 it just works *(它就是能用)* 。
- 使用 [supervisor](http://supervisord.org/), [pm2](https://pm2.keymetrics.io/), [monit](https://mmonit.com/monit/), [gosuv](https://github.com/codeskyblue/gosuv), 亦或是 [systemd](https://systemd.io/) 作为进程守护程序，保持 ArashiDNS.Aoi 运行。

## 协议兼容

### Google DNS over HTTPS Json API

当 `ct` 的程序参数不为 `dns-message`，且包含有效的 `name` 参数。 ArashiDNS.Aoi 将会提供 [Google JSON API for DNS over HTTPS (DoH)](https://developers.google.com/speed/public-dns/docs/doh/json) 的兼容协议。参数也同样相同，但是 `cd` , `do` , `random_padding` 没有被实现，传入也将被忽略。

### IETF RFC-8484 DNS over HTTPS

ArashiDNS.Aoi 包含较为完善的 [IETF DNS-over-HTTPS (RFC 8484)](https://tools.ietf.org/html/rfc8484) 兼容性。`GET` 请求中需要包含有效的 `dns` 参数。

### 特性

##### IPv6 支持

ArashiDNS.Aoi 包含完全的 IPv6 支持，但在很多时候，IPv4 仍是首选。你可能需要强制 `AAAA` 查询或手动设置 IPv6 监听地址。

##### EDNS-Client-Subnet

EDNS-Client-Subnet 已默认启用，但是您的上游源 DNS 服务器也需要支持 EDNS-Client-Subnet 才能生效。如果您的服务托管在 ECS 或处于 CDN 之后，请求头中需要包含 `X-Forwarded-For` 或 `X-Real-IP`。

如果你想要禁用 EDNS-Client-Subnet，请在您的客户端设置 EDNS-Client-Subnet IP 为 `0.0.0.0`。

## 反馈

- 作为一个初学者，可能存在非常多的问题，还请多多谅解。
- 如果有 Bug 或者希望新增功能，请在 issues 中提出。
- 如果你添加了新的功能或者修正了问题，也请向我提交 PR，非常感谢。

## 致谢

<img src='https://i.loli.net/2020/08/03/LWNj2BM6mxuYtRU.png' width="10%" height="10%" align="right"/>

> 我一直在使用 ReSharper，它真的可以说是令人惊叹的工具，使我的开发效率提升了数倍。

感谢 [JetBrains](https://www.jetbrains.com/?from=AuroraDNS) 为本项目提供了 [ReSharper](https://www.jetbrains.com/ReSharper/) 开源许可证授权。

## Credits 

如果没有开源软件与社区，就不会有 ArashiDNS.Aoi 的诞生。感谢那些支持开源的人们。

请查阅 [Credits](https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/CREDITS.md) ，其中包含了我们的协作者与使用到的其他开源软件。

## License

Copyright (c) 2020 Milkey Tan. Code released under the [Mozilla Public License 2.0](https://www.mozilla.org/en-US/MPL/2.0/). 
