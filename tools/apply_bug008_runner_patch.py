from pathlib import Path

p = Path("src/MailLoadTester.Core/SmtpTestRunner.cs")
t = p.read_text()
old = (
    "        if (options.DirectMxDelivery)\n"
    "        {\n"
    "            Report(0, 0, \"Resolving MX records…\", null, 1, \"DNS MX lookup\", \"SMTP spojení\",\n"
    "                pathStep: DeliveryStepKind.DnsMxLookup, pathOk: null);\n"
    "            mxRecords = await MxResolver.ResolveAsync(options.Recipients[0].Split('@').Last(), ct).ConfigureAwait(false);\n"
    "            var bestMx = MxResolver.GetBestHost(mxRecords);\n"
    "            options = options with { SmtpHost = bestMx };\n"
    "            Report(0, 0, $\"MX: {bestMx}\", null, 1, $\"MX resolved: {bestMx}\", \"SMTP spojení\",\n"
    "                pathStep: DeliveryStepKind.DnsMxLookup, pathOk: true);\n"
    "        }"
)
new = (
    "        if (options.DirectMxDelivery)\n"
    "        {\n"
    "            // BUG-008: never take Recipients[0] alone — require a single shared domain\n"
    "            // (defense in depth even if Validation.Validate was skipped).\n"
    "            var mxDomain = DirectMxRouting.RequireSingleRecipientDomain(options.Recipients);\n"
    "            Report(0, 0, $\"Resolving MX records for {mxDomain}…\", null, 1, \"DNS MX lookup\", \"SMTP spojení\",\n"
    "                pathStep: DeliveryStepKind.DnsMxLookup, pathOk: null);\n"
    "            mxRecords = await MxResolver.ResolveAsync(mxDomain, ct).ConfigureAwait(false);\n"
    "            var bestMx = MxResolver.GetBestHost(mxRecords);\n"
    "            options = options with { SmtpHost = bestMx };\n"
    "            Report(0, 0, $\"MX: {bestMx} ({mxDomain})\", null, 1, $\"MX resolved: {bestMx}\", \"SMTP spojení\",\n"
    "                pathStep: DeliveryStepKind.DnsMxLookup, pathOk: true);\n"
    "        }"
)
if "RequireSingleRecipientDomain" in t and "Recipients[0].Split" not in t:
    print("already patched")
elif old not in t:
    raise SystemExit("target block not found")
else:
    p.write_text(t.replace(old, new, 1))
    print("patched ok")
