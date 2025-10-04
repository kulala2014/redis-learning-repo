# Redis Mastery Roadmap for .NET Developers

> A structured, hands-on Redis learning plan designed specifically for .NET engineers who want to move from “I know what Redis is” to building production-grade solutions.

---

## 🎯 Goals

- Understand Redis fundamentals, data structures, and essential commands
- Learn persistence, replication, Sentinel, and Cluster mechanics
- Design robust caching, distributed locking, rate limiting, and messaging components in .NET
- Build an operational toolkit: testing, monitoring, profiling, and deployment

---

## 🗺️ High-Level Roadmap (4–6 weeks)

| Stage | Timeline | Focus Areas | Expected Deliverables |
|-------|----------|-------------|------------------------|
| **Phase 1** Fundamentals | Week 1 | Data structures, commands, persistence, transactions/Lua | Console demos, command cheat sheet, persistence comparison table |
| **Phase 2** Availability & Operations | Week 2–3 | Memory policies, replication, Sentinel, Cluster, performance diagnostics | Docker Compose setups, perf reports, architecture diagrams |
| **Phase 3** Production Patterns | Week 4–5 | Caching strategies, distributed locks, rate limiting, queues, monitoring | ASP.NET Core modules, Lua utility library, monitoring dashboards |
| **Phase 4** (Optional) | Week 6+ | Deep dive into internals and modules | Notes on redisObject, event loop, module experiments |

---

## 📆 Detailed Schedule

### Phase 1 · Foundations (Week 1)

| Day | Topic | Tasks | Acceptance Criteria |
|-----|-------|-------|---------------------|
| 1 | Architecture overview | Read Redis intro; sketch architecture; practice `SET/DEL/TTL` with `redis-cli` | 200-word summary |
| 2 | Strings & Hashes | Build a user caching demo with `StackExchange.Redis`; practice `GET/MSET/HSET` | Console demo committed |
| 3 | List/Set/ZSet | Create “recent posts,” “unique tags,” and “leaderboard” examples | Document command flows |
| 4 | Transactions & Lua | Experiment with `MULTI/EXEC`, Pipeline; write Lua script for stock deduction | C# app calling Lua script |
| 5 | Persistence | Compare RDB/AOF/hybrid; trigger each mode and observe files | Comparison table + screenshots |
| 6 | Review | Produce a command complexity cheat sheet; solve 10 practice scenarios | Markdown notes |
| 7 | Buffer Day | Close gaps; list remaining questions | Q&A checklist |

**Suggested resources**

- *Redis in Action* (selected chapters)
- *Redis Design and Implementation* (Part I)
- Redis docs: Data Types, Persistence
- YouTube/Bilibili series from “XiaoLin Coding”

---

### Phase 2 · High Availability & Ops (Week 2–3)

#### Week 2
- **Day 1**: Memory eviction experiment (`maxmemory` + LRU vs LFU vs Random)
- **Day 2**: Slowlog & Pipeline benchmarking; write a performance report
- **Day 3**: Master–Replica via Docker Compose; implement read/write splitting in .NET
- **Day 4**: Sentinel failover simulation
- **Day 5**: Redis Cluster setup (`redis-cli --cluster create`), resharding, FAQ notes

#### Week 3
- **Day 1**: Compare Watch-based optimistic locking vs Lua atomic updates
- **Day 2**: Diagnose hot keys using `--bigkeys` / `--hotkeys`
- **Day 3**: Security hardening (AUTH, TLS, ACL)
- **Day 4**: Produce a high availability architecture diagram
- **Day 5**: Mock interview: answer 10 HA-focused questions

**Suggested resources**

- *Redis Deep Dive* (中文版《Redis 深度历险》)
- Redis docs: Replication, Sentinel, Cluster
- GeekTime course “Redis Core Technology & Practice”

---

### Phase 3 · Production Patterns (Week 4–5)

#### Week 4 · Caching System
- Implement Cache-Aside in ASP.NET Core (with DB fallback)
- Mitigate cache penetration/breakdown: random TTLs, Bloom filters, mutex locking
- Build a reusable caching middleware with test coverage
- Document cache strategy decisions

#### Week 5 · Distributed Toolkit
- Distributed lock wrapper (SET NX + PX, Lua unlock); stress test retry logic
- Redis Stream task queue (producer/consumer, dead-letter handling)
- Lua-based sliding window rate limiter middleware
- Monitoring stack: RedisInsight, Prometheus exporter, Grafana dashboard

**Suggested resources**

- Cloud provider whitepapers (Alibaba, AWS, Azure) on Redis architecture
- Netflix/GitHub engineering blogs (Streams, throttling)
- Community libraries: Hangfire.Redis, CAP, StackExchange.Redis.Extensions

---

### Phase 4 · Optional Deep Dive

- Read Redis source (redisObject, event loop, SDS memory management)
- Explore Redis modules (Bloom filter, RedisJSON, RedisGraph)
- Write a minimal Redis module in C or Rust

---

## 🧰 Tooling for .NET Developers

| Category | Recommendation | Notes |
|----------|----------------|-------|
| Client | [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) | Officially maintained, high performance |
| Config | `IConfiguration` + Options | Environment-specific settings |
| Integration Tests | [Testcontainers for .NET](https://dotnet.testcontainers.org/) | Spin up ephemeral Redis instances |
| Profiling | MiniProfiler.Redis, `dotnet-monitor` | Analyze command latency |
| Visualization | RedisInsight, Another Redis Desktop Manager | GUI for browsing keys and slowlogs |
| Monitoring | Prometheus Redis Exporter + Grafana | Track QPS, latency, memory usage |

---

## 📚 Recommended Books & Courses

- *Redis Design and Implementation* — deep understanding of data structures and internals
- *Redis Deep Dive* (a.k.a. *Redis 深度历险*) — practical scenarios + internals
- *Redis in Action* (Manning) — project-oriented approach
- GeekTime “Redis Core Technology & Practice” course
- MIT 6.824 distributed systems lectures (replication & consistency)

---

## ✅ Progress Tracking Templates

```markdown
### Week 1 Checklist
- [ ] Understand Redis memory model and command categories
- [ ] Build StackExchange.Redis console demo
- [ ] Execute Lua stock decrement script from C#
- [ ] Create persistence comparison table

### Week 2 Checklist
- [ ] Run memory eviction experiments (LRU/LFU/Random)
- [ ] Set up Master + Sentinel failover
- [ ] Produce pipeline vs single-command performance report
- [ ] Document Cluster architecture Q&A

...
