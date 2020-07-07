
<p align="center">
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi'><img src='https://mili.one/pics/arashiaoi.png' width="50%" height="50%"/></a></p>
<p align="center">
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/README-zhs.md'><img src='https://mili.one/shields/zh-Hans-README.svg' alt='中文简体 Readme' referrerPolicy='no-referrer' /></a>
          <a href='https://github.com/mili-tan/ArashiDNS.Aoi/blob/master/README-zht.md'><img src='https://mili.one/shields/zh-Hant-README.svg' alt='中文繁體/正體 Readme' referrerPolicy='no-referrer' /></a>
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

It's that easy. Use `--help` / `-?` to discover more parameters and get help information.

## Introduces

#### Parameters

| parameter              | explanation                                                  | example                       |
| ---------------------- | ------------------------------------------------------------ | ----------------------------- |
| `-?` / `-h` / `--help` | Show help information                                        |                               |
| `-l` / `--listen`      | Set the server listening address and port                    | 127.0.0.1:2020                |
| `-u` / `--upstream`    | Set the upstream origin DNS server IP address                | 8.8.8.8                       |
| `-t` / `--timeout`     | Set timeout for query to the upstream DNS server (ms)        | 500                           |
| `-p` / `--perfix`      | Set your DNS over HTTPS server query prefix                  | "/dns-query"                |
| `-c` / `--cache`       | Local query cache settings                                   | `full` / `flexible` / `none`  |
| `--log`                | Console log output settings                                  | `full` / `dns-query` / `none` |
| `--tcp`                | Set enable upstream DNS query using TCP only                 |                               |
| `-s` / `--https`       | Set enable HTTPS (Self-Signed Certificate by default, **Not Recommended**) |                               |
| `-pfx` / `--pfxfile` | Set your .pfx certificate file path (with optional password) |"./cert.pfx*@passw0rd*"|

## Protocol Compatibility

### Google DNS over HTTPS Json API

When the `ct` parameter's application is not `dns-message` , and with a valid `name` parameter. ArashiDNS.Aoi provides [Google JSON API for DNS over HTTPS (DoH)](https://developers.google.com/speed/public-dns/docs/doh/json) compatible protocol. Parameters are also same, But `cd` , `do` , `random_padding` are not be implemented, they will be ignored.

### IETF RFC-8484 DNS over HTTPS

ArashiDNS.Aoi contains the complete [IETF DNS-over-HTTPS (RFC 8484)](https://tools.ietf.org/html/rfc8484) Compatibility. The `GET` request needs to contain valid `dns` parameters.

### Features

##### IPv6 Support 

Full IPv6 support is available, but in many cases IPv4 is still preferred. You may need to force `AAAA` lookups or ipv6 server listening addresse.

##### EDNS-Client-Subnet

EDNS-Client-Subnet is enabled by default. Your upstream origin DNS server also needs to support EDNS-Client-Subnet just to work. If your server is hosted in ECS or behind CDN, The request need include `X-Forwarded-For` or `X-Real-IP`. 

If you wish to disable it, please enter EDNS-Client-Subnet IP `0.0.0.0` in your client.

## Feedback

- As a starter, there may be many problems, so please forgive me.
- If you have bug report or feature request, please send issues.
- If you have added new feature or fixed bug, please submit a PR to me as well, thank you very much.
- I'm not a native English speaker, so forgive my typo and grammatical errors. If possible, please talk with me in Chinese.

## Credits 

ArashiDNS was born of open source softwares and the people who support it.

Check out Credits for a list of our collaborators and other open source softwares used.

## License

Copyright (c) 2020 Milkey Tan. Code released under the [Mozilla Public License 2.0](https://github.com/mili-tan/AuroraDNS.GUI/blob/master/LICENSE.md). 
