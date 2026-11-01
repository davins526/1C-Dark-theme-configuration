# Проверка обнаружения Defender

Обнаружение 0.2.2 остаётся открытым: Wacatac.B!ml у приложения,
Wacatac.C!ml у тестового процесса. Нельзя заранее утверждать, что оно ложное.

## Проверяемая сборка

Workflow **Verify Windows build and Defender scan** выполняется при изменении
исходников в main или вручную через вкладку Actions. Он предназначен для свежего
GitHub-hosted Windows Server 2022, а не для запуска на рабочем компьютере.

1. Проверяет логику отказа на обычных текстовых файлах с подставными ответами
   сканера. Это не антивирусная проверка приложения.
2. Обновляет базы Defender, требует активной защиты и баз не старше двух суток.
   Отсутствующий/неработающий Defender завершает job ошибкой; защита не включается
   и не отключается скриптом, исключения не меняются.
3. Проверяет подпись Microsoft у системного C#-компилятора. Скачивает официальный
   Zig 0.14.1 и сверяет закреплённый SHA-256 архива до его использования.
4. Собирает исходники и нейтральный фон. Личные файлы не загружаются.
   Полученные EXE/DLL не исполняются, подключения к 1С нет.
5. Проверяет, что файлы не исключены из сканирования, записывает SHA-256,
   выполняет CustomScan для DLL, приложения и консольного помощника. Проверяет
   историю обнаружений для этих путей, наличие и неизменность файлов после
   сканирования, повторно проверяет защиту и исключения.
6. Сохраняет только JSON-отчёты. Бинарники не публикуются. Карантин, ошибка
   сканера/журнала/проверки исключений или изменение файла запрещают успешный итог.

Отчёты: `build.json` содержит коммит, этап и хеш архива Zig;
`defender.json`, если этап достигнут, — хеши файлов, версию движка/баз и итог.
Статусы `unverified` и `detected` означают неуспешную проверку. Даже
`no-detection-in-this-scan` относится только к указанным байтам и сканеру в этой
среде. Это не заключение Microsoft и не проверка поведения работающей программы.
При неудаче до сканирования отчёта `defender.json` может не быть.

GitHub может предоставить образ без доступного Defender. Такой результат нужно
рассматривать как отсутствие проверки, а не как отсутствие угрозы. Нельзя
ослаблять проверки ради зелёной отметки. Повторяемость SHA сборки пока не доказана.

## Разбор Microsoft и подпись

Официальный путь оспаривания — [отправка файла как Software developer](https://www.microsoft.com/en-us/wdsi/filesubmission).
Для заявки нужны конкретный образец, его SHA-256, название обнаружения и описание
назначения. Не следует подменять заблокированный образец новым с другим хешем:
результат анализа относится к отправленным байтам. Если доступ к файлу блокируется,
не восстанавливайте его и не меняйте защиту ради этой инструкции.

Контекст для заявки (уточнить коммит/хеш и фактические результаты перед отправкой):

> Dark1C is an experimental open-source visual customization tool for the Windows
> 1C Configurator. Source: https://github.com/davins526/1C-Dark-theme-configuration.
> A local preview build was detected as Trojan:Win32/Wacatac.B!ml; a test executable
> was detected as Trojan:Win32/Wacatac.C!ml. We request analysis and a determination,
> not an allowlist without review. The application intentionally loads its own DLL
> into a user-selected, version-checked 1cv8 DESIGNER process using LoadLibraryW and
> CreateRemoteThread, then hooks drawing APIs with MinHook. OFF restores original
> drawing behavior; the DLL remains loaded until the target process exits.
> The reviewed source contains no network client, credential collection, database
> content access, persistence or antivirus configuration changes. This internal
> source review does not establish that the detected binary matches that source.

Образец и журналы автоматически не отправляются. Пока заявки и ответа нет.
Публиковать «Microsoft подтвердил ложное срабатывание» можно только после ответа
с привязкой к образцу и результату проверки.

Authenticode-подпись требует настоящего сертификата издателя и защиты закрытого
ключа; сертификата у проекта сейчас нет. Самоподписанный сертификат с установкой
корня пользователю не решает задачу. Подпись подтверждает издателя и целостность,
но не гарантирует отсутствие обнаружения Defender. Открытые вопросы загрузчика
и повышенных прав перечислены в [разборе исходников](SECURITY-REVIEW-2026-11-01.md).

Документация Microsoft: [CustomScan](https://learn.microsoft.com/en-us/powershell/module/defender/start-mpscan),
[проверка исключений](https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-antivirus-exclusions-configure),
[оспаривание обнаружения](https://learn.microsoft.com/en-us/defender-xdr/submission-guide).
