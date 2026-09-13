# Queue metrics (A3) evidence

- `ScenarioQueueMetrics` declared at `RunSingleAsync` method scope (not inside try).
- Wired: enqueue / dequeue / complete / cancel / full-wait / drain.
- `MailTestResult.QueueMetrics` snapshot on return.
- Fix commit: method-scope declaration (CS0103).
- Local: build + 273 tests passed before push.
