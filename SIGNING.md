# Подпись релиза и Microsoft Defender SmartScreen

## Что подпись действительно решает

Authenticode подтверждает автора и целостность `SensitivityRandomizer.exe` и
`SensitivityResetGuard.exe`. Вместо `Unknown publisher` Windows показывает
проверенное имя владельца сертификата. Одинаковая удостоверенная подпись на
последовательных релизах позволяет накапливать репутацию издателя.

Подпись не гарантирует мгновенного исчезновения SmartScreen. Microsoft
оценивает отдельно репутацию издателя и хеша конкретного файла. Даже новый
файл с действительной OV/EV или Artifact Signing подписью сначала может
показываться как незнакомый. Самоподписанный сертификат для публичного релиза
не помогает.

Единственный официальный способ исключить SmartScreen download warning с
первой установки: публикация MSIX через Microsoft Store, где пакет
переподписывает Microsoft.

Актуальные первичные источники:

- [SmartScreen reputation for Windows app developers](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)
- [Code signing options for Windows app developers](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options)
- [Microsoft Artifact Signing](https://learn.microsoft.com/azure/artifact-signing/)

## Что подписывать

Подписываются только файлы проекта:

- `SensitivityRandomizer.exe`
- `SensitivityResetGuard.exe`

Не переподписывай `wrapper.dll` или `Newtonsoft.Json.dll` от имени автора
программы: это сторонние библиотеки. После подписи всегда пересчитывай
`SHA256SUMS.txt`, потому что Authenticode меняет байты EXE.

## Бесплатный вариант для публичного GitHub-проекта

[SignPath Foundation](https://signpath.org/) предоставляет бесплатную
подпись подходящим open-source проектам. На текущем этапе Sensitivity
Randomizer соответствует базовым техническим условиям: MIT-лицензия,
открытый исходный код и MIT-зависимости. Однако Foundation требует уже
опубликованный и поддерживаемый проект и отдельно рассматривает заявку.

Это наиболее рациональный бесплатный путь после первого GitHub-релиза.
Подписание выполняется в проверяемом CI-процессе SignPath, а не локальным
`sign-release.ps1`. Одобрение заявки и отсутствие SmartScreen prompt нельзя
гарантировать заранее.

## Вариант 1: публичный OV-сертификат

Сертификат должен поддерживать Code Signing, строить доверенную Windows цепочку
и иметь доступный закрытый ключ. Импортируй сертификат в `CurrentUser\My` или
подключи выданный CA hardware/cloud HSM. Команды выполняются из корня исходного
проекта, в готовом ZIP это каталог `source`. Затем собери программу и запусти:

```powershell
.\build.ps1 -Configuration Release
.\sign-release.ps1 -CertificateThumbprint "THUMBPRINT"
```

Для сертификата в хранилище компьютера добавь:

```powershell
-CertificateStoreLocation LocalMachine
```

Скрипт намеренно отклоняет очевидный self-signed сертификат, использует
SHA-256 и RFC 3161 timestamp, проверяет обе подписи через SignTool и Windows,
затем пересчитывает контрольные суммы.

## Вариант 2: Microsoft Artifact Signing

После создания Public Trust profile, назначения роли signer и установки
Artifact Signing Client Tools создай локальный `metadata.json`:

```json
{
  "Endpoint": "https://weu.codesigning.azure.net",
  "CodeSigningAccountName": "ACCOUNT_NAME",
  "CertificateProfileName": "PROFILE_NAME"
}
```

Не добавляй реальный `metadata.json` или данные входа в Git. После сборки:

```powershell
.\sign-release.ps1 `
  -ArtifactSigningMetadata ".\metadata.json" `
  -ArtifactSigningDlib "C:\path\x64\Azure.CodeSigning.Dlib.dll"
```

Для Artifact Signing необходима успешная проверка личности/организации и
авторизация Azure. Скрипт не хранит сертификат или секреты в репозитории.

## Финальная проверка

На чистой Windows-машине открой свойства каждого EXE, вкладку
`Digital Signatures`, затем проверь:

```powershell
Get-AuthenticodeSignature .\SensitivityRandomizer.exe | Format-List Status,StatusMessage,SignerCertificate
Get-AuthenticodeSignature .\SensitivityResetGuard.exe | Format-List Status,StatusMessage,SignerCertificate
```

У обеих подписей нужен `Status: Valid`. Сохраняй одну и ту же удостоверенную
identity между версиями. Смена сертификата или выпуск неподписанного EXE
ухудшают перенос репутации на новые релизы.
