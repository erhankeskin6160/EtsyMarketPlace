namespace EtsyMarketPlace.Infrastructure.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using Microsoft.Data.Sqlite;

public sealed class SqliteShopResearchRepository : IShopResearchRepository
{
    private readonly string _connectionString;

    public SqliteShopResearchRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS shop_dna_profiles (
                owner_user_id TEXT NOT NULL,
                shop_id TEXT NOT NULL,
                dna_version INTEGER NOT NULL,
                primary_niche TEXT NOT NULL,
                target_audience TEXT NOT NULL,
                target_price_min_usd REAL NOT NULL,
                target_price_max_usd REAL NOT NULL,
                min_net_margin_pct REAL NOT NULL,
                estimated_cogs_ratio REAL NOT NULL,
                supports_3d_print INTEGER NOT NULL,
                preferred_materials_json TEXT NOT NULL,
                top_performing_tags_json TEXT NOT NULL,
                category_keywords_json TEXT NOT NULL,
                forbidden_keywords_json TEXT NOT NULL,
                schedule_cadence INTEGER NOT NULL,
                last_scheduled_run_at_utc TEXT NULL,
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY (owner_user_id, shop_id)
            );

            CREATE TABLE IF NOT EXISTS research_runs (
                run_id TEXT PRIMARY KEY,
                owner_user_id TEXT NOT NULL,
                shop_id TEXT NOT NULL,
                trigger_type INTEGER NOT NULL,
                status INTEGER NOT NULL,
                started_at_utc TEXT NOT NULL,
                completed_at_utc TEXT NULL,
                search_queries_json TEXT NOT NULL,
                hypotheses_json TEXT NOT NULL,
                summary TEXT NOT NULL,
                error_message TEXT NULL,
                exploration_query_used TEXT NULL,
                effective_shop_fee_rate_pct REAL NOT NULL DEFAULT 13.1
            );
            CREATE INDEX IF NOT EXISTS ix_research_runs_owner_shop ON research_runs (owner_user_id, shop_id, started_at_utc DESC);

            CREATE TABLE IF NOT EXISTS research_candidates (
                candidate_id TEXT PRIMARY KEY,
                run_id TEXT NOT NULL,
                owner_user_id TEXT NOT NULL,
                shop_id TEXT NOT NULL,
                title TEXT NOT NULL,
                suggested_title TEXT NOT NULL,
                suggested_tags_json TEXT NOT NULL,
                source_platform TEXT NOT NULL,
                source_url TEXT NOT NULL,
                category TEXT NOT NULL,
                estimated_sell_price_usd REAL NOT NULL,
                estimated_net_profit_usd REAL NOT NULL,
                estimated_net_margin_pct REAL NOT NULL,
                competition_count INTEGER NOT NULL,
                demand_signal_score REAL NOT NULL,
                opportunity_score INTEGER NOT NULL,
                risk_score INTEGER NOT NULL,
                cannibalization_score INTEGER NOT NULL,
                cannibalization_warning TEXT NULL,
                rationale TEXT NOT NULL,
                evidence_json TEXT NOT NULL,
                decision_status INTEGER NOT NULL,
                rejection_reason INTEGER NOT NULL,
                decided_at_utc TEXT NULL,
                net_profit_offsite12_usd REAL NOT NULL DEFAULT 0,
                net_profit_offsite15_usd REAL NOT NULL DEFAULT 0,
                effective_fee_rate_pct REAL NOT NULL DEFAULT 13.1,
                demand_score INTEGER NOT NULL DEFAULT 50,
                demand_confidence INTEGER NOT NULL DEFAULT 1,
                risk_decision INTEGER NOT NULL DEFAULT 0,
                risk_reasons_json TEXT NOT NULL DEFAULT '[]',
                is_exploration_candidate INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_research_candidates_run ON research_candidates (run_id);
            CREATE INDEX IF NOT EXISTS ix_research_candidates_owner_shop ON research_candidates (owner_user_id, shop_id);

            CREATE TABLE IF NOT EXISTS candidate_decision_events (
                event_id TEXT PRIMARY KEY,
                candidate_id TEXT NOT NULL,
                owner_user_id TEXT NOT NULL,
                shop_id TEXT NOT NULL,
                candidate_title TEXT NOT NULL,
                category TEXT NOT NULL,
                tags_json TEXT NOT NULL,
                decision INTEGER NOT NULL,
                reason INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_decision_events_owner_shop ON candidate_decision_events (owner_user_id, shop_id, created_at_utc DESC);
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        await EnsureColumnAsync(conn, "research_runs", "exploration_query_used", "TEXT NULL", ct);
        await EnsureColumnAsync(conn, "research_runs", "effective_shop_fee_rate_pct", "REAL NOT NULL DEFAULT 13.1", ct);
        await EnsureColumnAsync(conn, "research_candidates", "net_profit_offsite12_usd", "REAL NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync(conn, "research_candidates", "net_profit_offsite15_usd", "REAL NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync(conn, "research_candidates", "effective_fee_rate_pct", "REAL NOT NULL DEFAULT 13.1", ct);
        await EnsureColumnAsync(conn, "research_candidates", "demand_score", "INTEGER NOT NULL DEFAULT 50", ct);
        await EnsureColumnAsync(conn, "research_candidates", "demand_confidence", "INTEGER NOT NULL DEFAULT 1", ct);
        await EnsureColumnAsync(conn, "research_candidates", "risk_decision", "INTEGER NOT NULL DEFAULT 0", ct);
        await EnsureColumnAsync(conn, "research_candidates", "risk_reasons_json", "TEXT NOT NULL DEFAULT '[]'", ct);
        await EnsureColumnAsync(conn, "research_candidates", "is_exploration_candidate", "INTEGER NOT NULL DEFAULT 0", ct);
    }

    private static async Task EnsureColumnAsync(SqliteConnection conn, string table, string column, string definition, CancellationToken ct)
    {
        try
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            await alter.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException)
        {
            // Column already exists
        }
    }

    public async Task<ShopDnaProfile?> GetDnaProfileAsync(string ownerUserId, string shopId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT owner_user_id, shop_id, dna_version, primary_niche, target_audience,
                   target_price_min_usd, target_price_max_usd, min_net_margin_pct, estimated_cogs_ratio,
                   supports_3d_print, preferred_materials_json, top_performing_tags_json,
                   category_keywords_json, forbidden_keywords_json, schedule_cadence,
                   last_scheduled_run_at_utc, updated_at_utc
            FROM shop_dna_profiles
            WHERE owner_user_id = @owner AND shop_id = @shop;
            """;
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapDnaProfile(reader) : null;
    }

    public async Task UpsertDnaProfileAsync(ShopDnaProfile profile, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO shop_dna_profiles (
                owner_user_id, shop_id, dna_version, primary_niche, target_audience,
                target_price_min_usd, target_price_max_usd, min_net_margin_pct, estimated_cogs_ratio,
                supports_3d_print, preferred_materials_json, top_performing_tags_json,
                category_keywords_json, forbidden_keywords_json, schedule_cadence,
                last_scheduled_run_at_utc, updated_at_utc
            ) VALUES (
                @owner, @shop, @ver, @niche, @audience,
                @pmin, @pmax, @margin, @cogs,
                @s3d, @materials, @tags,
                @catkw, @forbidden, @cadence,
                @lastrun, @updated
            )
            ON CONFLICT(owner_user_id, shop_id) DO UPDATE SET
                dna_version = excluded.dna_version,
                primary_niche = excluded.primary_niche,
                target_audience = excluded.target_audience,
                target_price_min_usd = excluded.target_price_min_usd,
                target_price_max_usd = excluded.target_price_max_usd,
                min_net_margin_pct = excluded.min_net_margin_pct,
                estimated_cogs_ratio = excluded.estimated_cogs_ratio,
                supports_3d_print = excluded.supports_3d_print,
                preferred_materials_json = excluded.preferred_materials_json,
                top_performing_tags_json = excluded.top_performing_tags_json,
                category_keywords_json = excluded.category_keywords_json,
                forbidden_keywords_json = excluded.forbidden_keywords_json,
                schedule_cadence = excluded.schedule_cadence,
                last_scheduled_run_at_utc = COALESCE(excluded.last_scheduled_run_at_utc, shop_dna_profiles.last_scheduled_run_at_utc),
                updated_at_utc = excluded.updated_at_utc;
            """;

        cmd.Parameters.AddWithValue("@owner", profile.OwnerUserId);
        cmd.Parameters.AddWithValue("@shop", profile.ShopId);
        cmd.Parameters.AddWithValue("@ver", profile.DnaVersion);
        cmd.Parameters.AddWithValue("@niche", profile.PrimaryNiche);
        cmd.Parameters.AddWithValue("@audience", profile.TargetAudience);
        cmd.Parameters.AddWithValue("@pmin", (double)profile.TargetPriceMinUsd);
        cmd.Parameters.AddWithValue("@pmax", (double)profile.TargetPriceMaxUsd);
        cmd.Parameters.AddWithValue("@margin", (double)profile.MinAcceptableNetMarginPct);
        cmd.Parameters.AddWithValue("@cogs", (double)profile.EstimatedCogsRatio);
        cmd.Parameters.AddWithValue("@s3d", profile.Supports3DPrint ? 1 : 0);
        cmd.Parameters.AddWithValue("@materials", JsonSerializer.Serialize(profile.PreferredMaterials));
        cmd.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(profile.TopPerformingTags));
        cmd.Parameters.AddWithValue("@catkw", JsonSerializer.Serialize(profile.CategoryKeywords));
        cmd.Parameters.AddWithValue("@forbidden", JsonSerializer.Serialize(profile.ForbiddenKeywords));
        cmd.Parameters.AddWithValue("@cadence", (int)profile.ScheduleCadence);
        cmd.Parameters.AddWithValue("@lastrun", (object?)profile.LastScheduledRunAtUtc?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated", profile.UpdatedAtUtc.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ShopDnaProfile>> GetScheduledProfilesAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT owner_user_id, shop_id, dna_version, primary_niche, target_audience,
                   target_price_min_usd, target_price_max_usd, min_net_margin_pct, estimated_cogs_ratio,
                   supports_3d_print, preferred_materials_json, top_performing_tags_json,
                   category_keywords_json, forbidden_keywords_json, schedule_cadence,
                   last_scheduled_run_at_utc, updated_at_utc
            FROM shop_dna_profiles
            WHERE schedule_cadence > 0;
            """;

        var list = new List<ShopDnaProfile>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapDnaProfile(reader));
        }
        return list;
    }

    public async Task UpdateLastScheduledRunAsync(string ownerUserId, string shopId, DateTime utcNow, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE shop_dna_profiles
            SET last_scheduled_run_at_utc = @ts
            WHERE owner_user_id = @owner AND shop_id = @shop;
            """;
        cmd.Parameters.AddWithValue("@ts", utcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveRunAsync(ResearchRun run, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO research_runs (
                    run_id, owner_user_id, shop_id, trigger_type, status,
                    started_at_utc, completed_at_utc, search_queries_json, hypotheses_json, summary, error_message,
                    exploration_query_used, effective_shop_fee_rate_pct
                ) VALUES (
                    @id, @owner, @shop, @trigger, @status,
                    @started, @completed, @queries, @hypotheses, @summary, @err,
                    @expq, @feerate
                )
                ON CONFLICT(run_id) DO UPDATE SET
                    status = excluded.status,
                    completed_at_utc = excluded.completed_at_utc,
                    search_queries_json = excluded.search_queries_json,
                    hypotheses_json = excluded.hypotheses_json,
                    summary = excluded.summary,
                    error_message = excluded.error_message,
                    exploration_query_used = excluded.exploration_query_used,
                    effective_shop_fee_rate_pct = excluded.effective_shop_fee_rate_pct;
                """;
            cmd.Parameters.AddWithValue("@id", run.RunId.ToString());
            cmd.Parameters.AddWithValue("@owner", run.OwnerUserId);
            cmd.Parameters.AddWithValue("@shop", run.ShopId);
            cmd.Parameters.AddWithValue("@trigger", (int)run.TriggerType);
            cmd.Parameters.AddWithValue("@status", (int)run.Status);
            cmd.Parameters.AddWithValue("@started", run.StartedAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("@completed", (object?)run.CompletedAtUtc?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@queries", JsonSerializer.Serialize(run.SearchQueriesUsed));
            cmd.Parameters.AddWithValue("@hypotheses", run.HypothesesJson);
            cmd.Parameters.AddWithValue("@summary", run.Summary);
            cmd.Parameters.AddWithValue("@err", (object?)run.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@expq", (object?)run.ExplorationQueryUsed ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@feerate", (double)run.EffectiveShopFeeRatePct);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        foreach (var c in run.Candidates)
        {
            await using var cCmd = conn.CreateCommand();
            cCmd.Transaction = (SqliteTransaction)tx;
            cCmd.CommandText = """
                INSERT OR REPLACE INTO research_candidates (
                    candidate_id, run_id, owner_user_id, shop_id, title, suggested_title, suggested_tags_json,
                    source_platform, source_url, category, estimated_sell_price_usd, estimated_net_profit_usd,
                    estimated_net_margin_pct, competition_count, demand_signal_score, opportunity_score,
                    risk_score, cannibalization_score, cannibalization_warning, rationale, evidence_json,
                    decision_status, rejection_reason, decided_at_utc,
                    net_profit_offsite12_usd, net_profit_offsite15_usd, effective_fee_rate_pct,
                    demand_score, demand_confidence, risk_decision, risk_reasons_json, is_exploration_candidate
                ) VALUES (
                    @cid, @rid, @owner, @shop, @title, @stitle, @stags,
                    @plat, @url, @cat, @price, @net,
                    @margin, @comp, @demand, @opp,
                    @risk, @cann, @cannw, @rat, @ev,
                    @dec, @rej, @dectime,
                    @off12, @off15, @feepct,
                    @dscore, @dconf, @rdec, @rreasons, @isexp
                );
                """;
            cCmd.Parameters.AddWithValue("@cid", c.CandidateId.ToString());
            cCmd.Parameters.AddWithValue("@rid", run.RunId.ToString());
            cCmd.Parameters.AddWithValue("@owner", c.OwnerUserId);
            cCmd.Parameters.AddWithValue("@shop", c.ShopId);
            cCmd.Parameters.AddWithValue("@title", c.Title);
            cCmd.Parameters.AddWithValue("@stitle", c.SuggestedTitle);
            cCmd.Parameters.AddWithValue("@stags", JsonSerializer.Serialize(c.SuggestedTags));
            cCmd.Parameters.AddWithValue("@plat", c.SourcePlatform);
            cCmd.Parameters.AddWithValue("@url", c.SourceUrl);
            cCmd.Parameters.AddWithValue("@cat", c.Category);
            cCmd.Parameters.AddWithValue("@price", (double)c.EstimatedSellPriceUsd);
            cCmd.Parameters.AddWithValue("@net", (double)c.EstimatedNetProfitUsd);
            cCmd.Parameters.AddWithValue("@margin", (double)c.EstimatedNetMarginPct);
            cCmd.Parameters.AddWithValue("@comp", c.CompetitionCount);
            cCmd.Parameters.AddWithValue("@demand", c.DemandSignalScore);
            cCmd.Parameters.AddWithValue("@opp", c.OpportunityScore);
            cCmd.Parameters.AddWithValue("@risk", c.RiskScore);
            cCmd.Parameters.AddWithValue("@cann", c.CannibalizationScore);
            cCmd.Parameters.AddWithValue("@cannw", (object?)c.CannibalizationWarning ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@rat", c.Rationale);
            cCmd.Parameters.AddWithValue("@ev", c.EvidenceJson);
            cCmd.Parameters.AddWithValue("@dec", (int)c.DecisionStatus);
            cCmd.Parameters.AddWithValue("@rej", (int)c.RejectionReason);
            cCmd.Parameters.AddWithValue("@dectime", (object?)c.DecidedAtUtc?.ToString("O") ?? DBNull.Value);
            cCmd.Parameters.AddWithValue("@off12", (double)c.NetProfitOffsiteAds12Usd);
            cCmd.Parameters.AddWithValue("@off15", (double)c.NetProfitOffsiteAds15Usd);
            cCmd.Parameters.AddWithValue("@feepct", (double)c.EffectiveFeeRatePct);
            cCmd.Parameters.AddWithValue("@dscore", c.DemandScore);
            cCmd.Parameters.AddWithValue("@dconf", (int)c.DemandConfidence);
            cCmd.Parameters.AddWithValue("@rdec", (int)c.RiskDecision);
            cCmd.Parameters.AddWithValue("@rreasons", JsonSerializer.Serialize(c.RiskReasons));
            cCmd.Parameters.AddWithValue("@isexp", c.IsExplorationCandidate ? 1 : 0);
            await cCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ResearchRun>> GetRunsAsync(string ownerUserId, string shopId, int limit = 20, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT run_id, owner_user_id, shop_id, trigger_type, status,
                   started_at_utc, completed_at_utc, search_queries_json, hypotheses_json, summary, error_message,
                   exploration_query_used, effective_shop_fee_rate_pct
            FROM research_runs
            WHERE owner_user_id = @owner AND shop_id = @shop
            ORDER BY started_at_utc DESC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100));

        var runs = new List<ResearchRun>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            runs.Add(MapRun(reader));
        }
        await reader.CloseAsync();

        if (runs.Count > 0)
        {
            runs[0].Candidates = await LoadCandidatesForRunAsync(conn, ownerUserId, shopId, runs[0].RunId, ct);
        }

        return runs;
    }

    public async Task<ResearchRun?> GetRunByIdAsync(string ownerUserId, string shopId, Guid runId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT run_id, owner_user_id, shop_id, trigger_type, status,
                   started_at_utc, completed_at_utc, search_queries_json, hypotheses_json, summary, error_message,
                   exploration_query_used, effective_shop_fee_rate_pct
            FROM research_runs
            WHERE run_id = @id AND owner_user_id = @owner AND shop_id = @shop;
            """;
        cmd.Parameters.AddWithValue("@id", runId.ToString());
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var run = MapRun(reader);
        await reader.CloseAsync();
        run.Candidates = await LoadCandidatesForRunAsync(conn, ownerUserId, shopId, run.RunId, ct);
        return run;
    }

    public async Task<ResearchCandidate?> GetCandidateByIdAsync(string ownerUserId, string shopId, Guid candidateId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = CandidateSelectSql + " WHERE candidate_id = @cid AND owner_user_id = @owner AND shop_id = @shop;";
        cmd.Parameters.AddWithValue("@cid", candidateId.ToString());
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapCandidate(reader) : null;
    }

    public async Task UpdateCandidateDecisionAsync(
        string ownerUserId,
        string shopId,
        Guid candidateId,
        CandidateDecisionStatus status,
        CandidateRejectionReason reason,
        CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE research_candidates
            SET decision_status = @dec, rejection_reason = @rej, decided_at_utc = @ts
            WHERE candidate_id = @cid AND owner_user_id = @owner AND shop_id = @shop;
            """;
        cmd.Parameters.AddWithValue("@dec", (int)status);
        cmd.Parameters.AddWithValue("@rej", (int)reason);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@cid", candidateId.ToString());
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AppendDecisionEventAsync(CandidateDecisionEvent decisionEvent, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO candidate_decision_events (
                event_id, candidate_id, owner_user_id, shop_id, candidate_title, category, tags_json, decision, reason, created_at_utc
            ) VALUES (
                @eid, @cid, @owner, @shop, @title, @cat, @tags, @dec, @rej, @ts
            );
            """;
        cmd.Parameters.AddWithValue("@eid", decisionEvent.EventId.ToString());
        cmd.Parameters.AddWithValue("@cid", decisionEvent.CandidateId.ToString());
        cmd.Parameters.AddWithValue("@owner", decisionEvent.OwnerUserId);
        cmd.Parameters.AddWithValue("@shop", decisionEvent.ShopId);
        cmd.Parameters.AddWithValue("@title", decisionEvent.CandidateTitle);
        cmd.Parameters.AddWithValue("@cat", decisionEvent.Category);
        cmd.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(decisionEvent.Tags));
        cmd.Parameters.AddWithValue("@dec", (int)decisionEvent.Decision);
        cmd.Parameters.AddWithValue("@rej", (int)decisionEvent.Reason);
        cmd.Parameters.AddWithValue("@ts", decisionEvent.CreatedAtUtc.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<CandidateDecisionEvent>> GetRecentDecisionEventsAsync(string ownerUserId, string shopId, int limit = 25, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT event_id, candidate_id, owner_user_id, shop_id, candidate_title, category, tags_json, decision, reason, created_at_utc
            FROM candidate_decision_events
            WHERE owner_user_id = @owner AND shop_id = @shop
            ORDER BY created_at_utc DESC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 100));

        var list = new List<CandidateDecisionEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CandidateDecisionEvent
            {
                EventId = Guid.Parse(reader.GetString(0)),
                CandidateId = Guid.Parse(reader.GetString(1)),
                OwnerUserId = reader.GetString(2),
                ShopId = reader.GetString(3),
                CandidateTitle = reader.GetString(4),
                Category = reader.GetString(5),
                Tags = DeserializeStrings(reader.GetString(6)),
                Decision = (CandidateDecisionStatus)reader.GetInt32(7),
                Reason = (CandidateRejectionReason)reader.GetInt32(8),
                CreatedAtUtc = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }
        return list;
    }

    private const string CandidateSelectSql = """
        SELECT candidate_id, run_id, owner_user_id, shop_id, title, suggested_title, suggested_tags_json,
               source_platform, source_url, category, estimated_sell_price_usd, estimated_net_profit_usd,
               estimated_net_margin_pct, competition_count, demand_signal_score, opportunity_score,
               risk_score, cannibalization_score, cannibalization_warning, rationale, evidence_json,
               decision_status, rejection_reason, decided_at_utc,
               net_profit_offsite12_usd, net_profit_offsite15_usd, effective_fee_rate_pct,
               demand_score, demand_confidence, risk_decision, risk_reasons_json, is_exploration_candidate
        FROM research_candidates
        """;

    private async Task<IReadOnlyList<ResearchCandidate>> LoadCandidatesForRunAsync(
        SqliteConnection conn, string ownerUserId, string shopId, Guid runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = CandidateSelectSql + " WHERE run_id = @rid AND owner_user_id = @owner AND shop_id = @shop ORDER BY opportunity_score DESC;";
        cmd.Parameters.AddWithValue("@rid", runId.ToString());
        cmd.Parameters.AddWithValue("@owner", ownerUserId);
        cmd.Parameters.AddWithValue("@shop", shopId);

        var list = new List<ResearchCandidate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapCandidate(reader));
        }
        return list;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        await pragma.ExecuteNonQueryAsync(ct);
        return conn;
    }

    private static ShopDnaProfile MapDnaProfile(SqliteDataReader r) => new()
    {
        OwnerUserId = r.GetString(0),
        ShopId = r.GetString(1),
        DnaVersion = r.GetInt32(2),
        PrimaryNiche = r.GetString(3),
        TargetAudience = r.GetString(4),
        TargetPriceMinUsd = (decimal)r.GetDouble(5),
        TargetPriceMaxUsd = (decimal)r.GetDouble(6),
        MinAcceptableNetMarginPct = (decimal)r.GetDouble(7),
        EstimatedCogsRatio = (decimal)r.GetDouble(8),
        Supports3DPrint = r.GetInt32(9) == 1,
        PreferredMaterials = DeserializeStrings(r.GetString(10)),
        TopPerformingTags = DeserializeStrings(r.GetString(11)),
        CategoryKeywords = DeserializeStrings(r.GetString(12)),
        ForbiddenKeywords = DeserializeStrings(r.GetString(13)),
        ScheduleCadence = (ResearchScheduleCadence)r.GetInt32(14),
        LastScheduledRunAtUtc = r.IsDBNull(15) ? null : DateTime.Parse(r.GetString(15), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        UpdatedAtUtc = DateTime.Parse(r.GetString(16), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    private static ResearchRun MapRun(SqliteDataReader r) => new()
    {
        RunId = Guid.Parse(r.GetString(0)),
        OwnerUserId = r.GetString(1),
        ShopId = r.GetString(2),
        TriggerType = (ResearchTriggerType)r.GetInt32(3),
        Status = (ResearchRunStatus)r.GetInt32(4),
        StartedAtUtc = DateTime.Parse(r.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        CompletedAtUtc = r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        SearchQueriesUsed = DeserializeStrings(r.GetString(7)),
        HypothesesJson = r.GetString(8),
        Summary = r.GetString(9),
        ErrorMessage = r.IsDBNull(10) ? null : r.GetString(10),
        ExplorationQueryUsed = r.IsDBNull(11) ? null : r.GetString(11),
        EffectiveShopFeeRatePct = (decimal)r.GetDouble(12)
    };

    private static ResearchCandidate MapCandidate(SqliteDataReader r) => new()
    {
        CandidateId = Guid.Parse(r.GetString(0)),
        RunId = Guid.Parse(r.GetString(1)),
        OwnerUserId = r.GetString(2),
        ShopId = r.GetString(3),
        Title = r.GetString(4),
        SuggestedTitle = r.GetString(5),
        SuggestedTags = DeserializeStrings(r.GetString(6)),
        SourcePlatform = r.GetString(7),
        SourceUrl = r.GetString(8),
        Category = r.GetString(9),
        EstimatedSellPriceUsd = (decimal)r.GetDouble(10),
        EstimatedNetProfitUsd = (decimal)r.GetDouble(11),
        EstimatedNetMarginPct = (decimal)r.GetDouble(12),
        CompetitionCount = r.GetInt32(13),
        DemandSignalScore = r.GetDouble(14),
        OpportunityScore = r.GetInt32(15),
        RiskScore = r.GetInt32(16),
        CannibalizationScore = r.GetInt32(17),
        CannibalizationWarning = r.IsDBNull(18) ? null : r.GetString(18),
        Rationale = r.GetString(19),
        EvidenceJson = r.GetString(20),
        DecisionStatus = (CandidateDecisionStatus)r.GetInt32(21),
        RejectionReason = (CandidateRejectionReason)r.GetInt32(22),
        DecidedAtUtc = r.IsDBNull(23) ? null : DateTime.Parse(r.GetString(23), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        NetProfitOffsiteAds12Usd = (decimal)r.GetDouble(24),
        NetProfitOffsiteAds15Usd = (decimal)r.GetDouble(25),
        EffectiveFeeRatePct = (decimal)r.GetDouble(26),
        DemandScore = r.GetInt32(27),
        DemandConfidence = (DemandConfidenceLevel)r.GetInt32(28),
        RiskDecision = (IpRiskDecision)r.GetInt32(29),
        RiskReasons = DeserializeStrings(r.GetString(30)),
        IsExplorationCandidate = r.GetInt32(31) == 1
    };

    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
