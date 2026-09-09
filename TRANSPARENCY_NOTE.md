# Application Card: SQL Nexus Diagnostic Agent

---

## What is an Application Card?

Microsoft's Application cards are intended to help you understand how our AI technology works, the choices application owners can make that influence application performance and behavior, and the importance of considering the whole application, including the technology, the people, and the environment. These resources can support the development or deployment of your own applications and can be shared with users or stakeholders impacted by them.

As part of its commitment to responsible AI, Microsoft values six core principles: fairness, reliability and safety, privacy and security, inclusiveness, transparency, and accountability. These principles are embedded in the Responsible AI Standard, which guides teams in designing, building, and testing AI applications. Application and Platform Cards play a key role in operationalizing these principles by offering transparency around capabilities, intended uses, and limitations. For further insight, readers are encouraged to explore Microsoft's [Responsible AI Transparency Report](https://www.microsoft.com/en-us/corporate-responsibility/responsible-ai-transparency-report) and [Code of Conduct](https://www.microsoft.com/en-us/legal/terms-of-use), which outline how enterprise customers and individuals can engage with AI responsibly.

---

## 1. Overview

The SQL Nexus Diagnostic Agent is an AI-assisted performance diagnostic tool for Microsoft SQL Server, designed for use by Microsoft Customer Service and Support (CSS) engineers and internal support staff. It helps engineers identify the root cause of SQL Server performance issues faster and more consistently by enabling natural language conversations over pre-collected, offline diagnostic data.

When a customer reports a SQL Server performance problem, an engineer uses SQL LogScout or PSSDiag to collect diagnostic data from the customer's server. That data is then imported into a local SQL Server database using the open-source SQL Nexus tool (github.com/microsoft/SqlNexus). Historically, the engineer would then spend two to eight hours manually querying this database and cross-referencing documentation to identify the root cause. The SQL Nexus Diagnostic Agent replaces this manual process with a natural language conversation: the engineer describes the symptom, and the agent autonomously calls the appropriate diagnostic tools, reasons over the results, and returns a root-cause analysis with specific data values and recommended actions — all within minutes.

The agent operates entirely on the engineer's local machine using pre-collected, offline data. It does not connect to any live customer environment, does not write to any database, and does not take any automated remediation actions. All findings are reviewed by the engineer before any action is taken.

---

## 2. Key Terms

The following list provides a glossary of key terms related to the SQL Nexus Diagnostic Agent:

**Agent:** An AI system that autonomously decides which tools to call and in what order to answer a user's question, based on reasoning about intermediate results. In this system, the agent is defined by a configuration file (`.agent.md`) and runs within GitHub Copilot in VS Code.

**GitHub Copilot:** A Microsoft AI coding and productivity assistant embedded in VS Code. It serves as the host for the SQL Nexus Diagnostic Agent, routing tool calls to the MCP server and sending results to the selected AI model.

**Model Context Protocol (MCP):** An open standard interface that allows AI models to call external tools and retrieve structured data. The SQL Nexus MCP Server implements this protocol, exposing diagnostic query tools to the agent.

**MCP Server:** A local executable (`SqlNexus.McpServer.exe`) that receives tool call requests from the agent over a standard input/output channel, executes read-only SQL queries against the SQL Nexus database, and returns structured JSON results.

**PII (Personally Identifiable Information):** Any data that could identify an individual, such as IP addresses, computer names, domain usernames, email addresses, or Windows file paths. The system applies automatic PII redaction to all tool outputs before they are passed to the AI model.

**PSSDiag / SQL LogScout:** Microsoft tools used to collect SQL Server diagnostic data from a customer's server. The collected data is imported into SQL Nexus for analysis. This collection happens before the agent is involved and is not part of the AI system.

**SQL Nexus:** An existing open-source Microsoft tool (github.com/microsoft/SqlNexus) that imports SQL Server diagnostic data into a local SQL Server database and provides reports for analysis. The AI agent reads from this database; it does not modify it.

**Wait Statistics:** A SQL Server mechanism that records how long server threads spent waiting for specific resources (CPU, disk, locks, memory). Wait statistics are the primary signal used to identify the type of performance bottleneck.

---

## 3. Key Features and Capabilities

The key features and capabilities outlined here describe what the SQL Nexus Diagnostic Agent is designed to do and how it performs across supported tasks.

The SQL Nexus Diagnostic Agent is an autonomous agentic AI system with a defined action space: it can call any of 35 read-only diagnostic tools, read local skill files, and search the local workspace. It cannot write data, execute commands, or access external networks. The agent plans and adapts its tool-calling sequence based on what each result reveals, following a hypothesis-driven approach rather than a fixed script.

- **Autonomous multi-step diagnostic reasoning:** The agent independently decides which tools to call and in what order, based on the reported symptom and what each result reveals. It forms a hypothesis, tests it with data, refines, and repeats — in the same way an experienced database administrator (DBA) would work through a case. The engineer does not need to know which specific queries to run.

- **35 read-only diagnostic tools:** The MCP server exposes 35 pre-built tools covering CPU analysis, wait statistics, blocking chain analysis, I/O performance, memory pressure, query performance, per-application breakdowns, missing index detection, and statistics health. Each tool translates a diagnostic question into a structured SQL query against the local SQL Nexus database and returns a structured JSON result.

- **Skill file cross-check:** After completing its initial free-form analysis, the agent consults 11 curated skill files containing expert SQL Server diagnostic decision trees, threshold values, and interpretation rules. This serves as a second opinion and completeness check, ensuring that no diagnostic angle is missed.

- **PII scrubbing before model inference:** All tool outputs pass through an automatic PII redaction layer before being sent to the AI model. This layer uses regex-based rules to redact IP addresses, computer names, domain usernames, email addresses, UNC paths, Windows file paths, SQL login names, GUIDs, and phone numbers — replacing each with a labeled placeholder.

- **Root-cause synthesis with data citations:** The agent concludes each diagnostic session with a written root-cause summary that cites the specific data values (wait counts, CPU percentages, query hashes, latency figures) that led to the conclusion, and provides prioritized recommended actions.

- **Custom SQL query fallback:** For diagnostic questions outside the 35 built-in tools, engineers can invoke the `query_nexus_database` tool with a custom SQL SELECT statement, allowing ad-hoc analysis without leaving the agent session.

---

## 4. Intended Uses

The SQL Nexus Diagnostic Agent can be used across a variety of SQL Server performance diagnostic scenarios. The agent has a defined action space — it is domain-specific, not general-purpose — scoped to read-only analysis of pre-collected SQL Server diagnostic data.

- **High CPU diagnosis:** A CSS engineer is working a support case where a customer reports that SQL Server is consuming 100% CPU. The engineer imports the customer's SQL LogScout collection into SQL Nexus, asks the agent "Is there high CPU on this system, and which queries are causing it?" The agent calls CPU analysis tools, identifies the sustained high-CPU periods, surfaces the top CPU-consuming queries by hash, and determines whether the cause is expensive query plans, excessive compilations, or parameter sniffing — in a fraction of the time it would take to do manually.

- **Blocking and deadlock investigation:** A customer reports that application transactions are timing out due to database blocking. The engineer asks the agent to analyze the blocking situation. The agent reconstructs the full blocking chain tree, identifies the head blocker session and the query it is running, determines the contested resource, and explains whether the fix is an index addition, a transaction scope change, or an application-level retry pattern.

- **I/O latency triage:** A customer reports that queries are slow and storage appears to be the bottleneck. The agent analyzes file-level I/O statistics and wait data, identifies whether SQL Server is the contributing factor to disk latency, determines which database files are affected, and finds the specific queries driving the read or write pressure.

- **General performance triage on unknown bottleneck:** An engineer receives a support case with no clear symptom description — only "SQL Server is slow." The agent starts with an overall performance summary, identifies the dominant wait category, and automatically narrows to the appropriate diagnostic path (CPU, I/O, blocking, or memory) based on what the data shows.

- **Before/after validation after a change:** After applying a patch or configuration change to a customer's SQL Server, an engineer wants to confirm whether performance improved. The agent analyzes two separate diagnostic collections (one before, one after) sequentially and compares key metrics — average wait times, top query costs, CPU utilization — to produce a structured comparison.

---

## 5. Models and Training Data

The SQL Nexus Diagnostic Agent leverages foundation models made available through [GitHub Copilot](https://docs.github.com/en/copilot/responsible-use-of-github-copilot-features/responsible-use-of-github-copilot-chat-in-your-ide). The engineer selects the model at the start of each session. Supported models available through GitHub Copilot include GPT-4o and o3 (via [Azure OpenAI Service](https://learn.microsoft.com/en-us/legal/cognitive-services/openai/transparency-note)) and Claude Sonnet (via Anthropic, accessed through GitHub Copilot's model gateway). To learn more about the data used to train these foundation models, refer to the linked transparency notes and model cards.

The SQL Nexus Diagnostic Agent itself does not train, fine-tune, or modify any AI model. No customer diagnostic data, SQL Server telemetry, or support case history is used as training input. The agent's domain knowledge is encoded in 11 hand-authored Markdown skill files written by experienced Microsoft support engineers; these files are read-only reference material used at inference time, not for training.

---

## 6. Performance

The SQL Nexus Diagnostic Agent is designed to perform reliably when used within its intended scope: analyzing pre-collected, offline SQL Server diagnostic data imported into a SQL Nexus database on the engineer's local machine. Performance is consistent across the supported diagnostic scenarios — CPU, blocking, I/O, memory, query-level analysis, and application-level breakdowns — provided the relevant data was captured during the SQL LogScout or PSSDiag collection.

Inputs to the system are natural language questions typed by the engineer in VS Code Copilot Chat (text only; no image, audio, or video input). Outputs are natural language analysis summaries and recommendations (text only), grounded in structured JSON data returned by the MCP diagnostic tools. The system does not produce executable scripts, configuration files, or any output that is automatically applied to any system.

The system was developed and evaluated in English. Diagnostic output is generated in the language of the conversation, but the skill files and tool descriptions are English-only. Engineers using the system in other languages may experience reduced quality in skill file cross-checks or tool selection.

The quality of diagnostic output depends on the quality and completeness of the underlying SQL LogScout collection. A thorough `GeneralPerf` or `DetailedPerf` collection covering the period of the performance issue will yield the most reliable results. Collections that missed the issue window, used a lighter scenario (such as `LightPerf`), or did not include a trace or XEvent session will result in some tools returning no data, which the agent will report to the engineer.

---

## 7. Limitations

Understanding the SQL Nexus Diagnostic Agent's limitations is crucial to determine if it is used within safe and effective boundaries. While we encourage users to leverage the SQL Nexus Diagnostic Agent in their diagnostic workflows, it's important to note that the SQL Nexus Diagnostic Agent was not designed for every possible scenario. We encourage users to refer to either the [Microsoft Enterprise AI Services Code of Conduct](https://www.microsoft.com/en-us/legal/terms-of-use) (for organizations) or the Code of Conduct section in the Microsoft Services Agreement (for individuals) as well as the following considerations when choosing a use case:

- **Collection scenario dependency:** The agent can only analyze data that was collected. If the wrong SQL LogScout scenario was run for the reported symptom, the required diagnostic tables may be absent and the corresponding tools will return no data. Engineers should verify that the collection scenario matches the symptom before starting an agent session, as the agent cannot retroactively collect missing data.

- **Query-level analysis requires trace data:** All query-level diagnostic tools depend on data from a SQL Trace or XEvent session captured during collection. A collection without tracing (for example, a metrics-only LogScout run) will have no query-level data, disabling the majority of the query analysis tools. Engineers should confirm trace data is present before expecting query-level insights.

- **No live or production server access:** The agent only analyzes pre-collected, offline data. It cannot observe a performance issue as it is occurring on a live server. For active, ongoing incidents, SQL LogScout must first be run against the production server and the resulting data imported into SQL Nexus before the agent can be used.

- **PII scrubbing is regex-based with bounded coverage:** The PII redaction layer catches structured, predictable identifiers (IP addresses, domain tokens, Windows file paths, email addresses, etc.). It will not catch customer business names or person names that appear as literal string values inside SQL query text, or identifier formats not matching the defined patterns. Engineers should not rely solely on automated scrubbing and should review agent outputs before sharing externally.

- **Execution plan analysis is not supported:** The agent surfaces plan cache metadata and compilation statistics but cannot parse or interpret query execution plan XML. Identifying bad plan shapes or join order issues requires the engineer to open the plan manually in SQL Server Management Studio (SSMS).

- **Output quality varies by model:** The agent was developed and validated primarily with Claude Sonnet and GPT-4o. Less capable models may miss multi-step diagnostic reasoning or draw incorrect conclusions from ambiguous data. Engineers should review all agent conclusions against the cited data values before taking action.

- **No automated remediation:** The agent is designed to produce analysis and recommendations only. It is explicitly constrained from applying configuration changes, executing commands, or modifying any database. All remediation is the responsibility of the engineer.

- **Windows only:** The MCP server executable targets .NET Framework 4.8 and runs only on Windows. It is not supported on Linux or macOS.

---

## 8. Evaluations

Performance and safety evaluations assess whether AI applications are operating reliably and securely by examining factors like groundedness, relevance, and coherence while identifying the risks of generating harmful content. The following evaluations were conducted with safety components already in place, which are also described in Section 9.

### 8.1 Performance and Quality Evaluations

The SQL Nexus Diagnostic Agent does not directly host or operate a foundation model — it relies on models provided and evaluated by GitHub Copilot (GPT-4o, o3, Claude Sonnet, and others available through the GitHub Copilot model gateway). Performance and quality evaluations for these models — including groundedness, coherence, fluency, and similarity — have been conducted by the respective model providers as part of their own Responsible AI review processes, which include Microsoft's TrIP (Trustworthy and Responsible Implementation Process) and OneRAI reviews for Azure OpenAI models, and equivalent processes for models accessed through GitHub Copilot's partner model gateway.

#### 8.1a Performance and Quality Evaluation Methods

For the foundation models powering this agent, performance and quality evaluations are documented in the model providers' transparency notes. For GPT-4o and Azure OpenAI models, refer to the [Azure OpenAI Service Transparency Note](https://learn.microsoft.com/en-us/legal/cognitive-services/openai/transparency-note), which covers groundedness (factual accuracy relative to grounded content), coherence (logical structure of outputs), fluency (linguistic quality), and similarity (alignment with reference outputs). These evaluations are conducted on text modality inputs and outputs, which is the only modality used by the SQL Nexus Diagnostic Agent. For Claude models accessed through GitHub Copilot, refer to [Anthropic's responsible AI documentation](https://www.anthropic.com/responsible-disclosure-policy).

The SQL Nexus Diagnostic Agent additionally benefits from the grounding constraint inherent in its design: all agent outputs are anchored to structured data returned by MCP tools from a local SQL Server database. The agent is instructed to cite specific numeric values from tool results in its conclusions, which provides a verifiable grounding check that engineers can apply manually — comparing the stated conclusion against the raw tool output data.

### 8.2 Risk and Safety Evaluations

Risk and safety evaluations for the foundation models used by this agent — including assessments for hate and unfairness, sexual content, violence, self-harm, protected material, direct jailbreak, and indirect jailbreak — have been conducted by the model providers as part of their Responsible AI review and certification processes. GitHub Copilot's models have passed Microsoft's internal RAI review (OneRAI) and TrIP processes where applicable. These evaluations cover the full range of content safety risks for the text modality.

#### 8.2a Risk and Safety Evaluation Methods

For Azure OpenAI models (GPT-4o, o3), risk and safety evaluation details are available in the [Azure OpenAI Service Transparency Note](https://learn.microsoft.com/en-us/legal/cognitive-services/openai/transparency-note) and the [Azure AI Content Safety documentation](https://learn.microsoft.com/en-us/azure/ai-services/content-safety/overview). Evaluations cover text-modality inputs and outputs and include both automated adversarial prompt testing and human evaluation against Microsoft's Responsible AI Standard. For Claude models, refer to [Anthropic's usage policies and safety documentation](https://www.anthropic.com/usage-policy).

The SQL Nexus Diagnostic Agent's exposure to content safety risks is structurally limited by its design: inputs to the model are SQL Server diagnostic data (numeric metrics, query text, wait statistics) passed as tool results, not open-ended user content. The agent does not generate creative content, does not process user-uploaded files, and does not engage in scenarios where hate, violence, self-harm, or sexual content would be contextually relevant. The primary content safety risk specific to this application — exposure of customer PII to the model — is addressed by the PII scrubbing layer described in Section 9.

### 8.3 Custom Evaluations

The SQL Nexus Diagnostic Agent was evaluated through structured manual testing against a real SQL LogScout collection imported into a SQL Nexus database representing a high-CPU SQL Server incident (`NexusDiagnosticsTest`).

Evaluation methodology: the agent was given the same natural language diagnostic prompts that an engineer would type in a real support case (for example, "Is there high CPU on this system? Which queries are causing it?"). The agent's tool-calling sequence, intermediate reasoning, and final conclusions were compared against the expected diagnostic path documented in the skill files and against the conclusions drawn by an experienced DBA analyzing the same dataset manually using SQL Nexus reports.

An ideal result is one where the agent: (1) calls the appropriate tools in a logical sequence without unnecessary or irrelevant tool calls, (2) correctly identifies the dominant bottleneck type and root cause, (3) cites specific numeric values from the data in its conclusion, and (4) provides actionable recommendations consistent with the skill file guidance. A suboptimal result is one where the agent calls an inappropriate tool, draws a conclusion not supported by the data, omits a significant diagnostic finding that would change the recommendation, or speculates when data is absent rather than reporting the data gap.

The `Test-McpServer.ps1` script provides functional validation at the MCP transport layer, confirming that the `get_top_cpu_queries`, `analyze_wait_stats`, and `get_top_queries_by_duration` tools return correctly structured JSON responses for a given database connection.

---

## 9. Safety Components and Mitigations

- **PII scrubbing on all tool outputs:** Every tool output from the MCP server passes through `PiiScrubber.cs` before being sent to the AI model. This pure C# implementation — with no external library dependencies — applies nine regex rules to redact IP addresses, computer names (WIN-\*, DESKTOP-\*), NT domain\username tokens, Windows user profile paths, UNC paths, email addresses, GUIDs, SQL login name JSON fields, and phone numbers. A URL allowlist additionally replaces non-approved URLs with a labeled placeholder. This mitigates the risk of customer PII from SQL Server diagnostic data reaching the AI model or appearing in agent outputs.

- **Read-only SQL access enforced at the tool layer:** The MCP server only accepts and executes `SELECT`, `WITH` (CTE), `DECLARE`, and `IF` statements. No `INSERT`, `UPDATE`, `DELETE`, `EXEC`, or DDL statements are issued by any tool, including the custom query tool `query_nexus_database`. This prevents any accidental or adversarial modification of the diagnostic database.

- **Offline data only — no production server connection:** The MCP server connects exclusively to the local SQL Nexus database on the engineer's machine. It has no mechanism to connect to a customer's live SQL Server instance. This architectural constraint eliminates the risk of the agent inadvertently querying or affecting a production environment.

- **No credentials stored in the repository:** The MCP server connection configuration (`mcp.json`) is gitignored and stored only on the engineer's local machine. The server uses Windows Integrated Authentication by default, meaning no passwords are stored or transmitted. If SQL authentication is used, credentials are supplied via environment variables or a local configuration file, never committed to source control.

- **Agent action space bounded to defined tools:** The agent's `.agent.md` definition restricts it to 35 named MCP tools plus `read` (local files) and `search` (local workspace). The agent has no shell execution capability, no internet access, no file write access, and no ability to install packages or run scripts. This limits the blast radius of any unexpected model behavior.

- **GitHub Copilot content safety:** The AI model inference layer is provided by GitHub Copilot, which applies Microsoft's content safety policies — including harmful content detection, jailbreak resistance, and prompt injection mitigations — to all model interactions. These controls are inherited from the GitHub Copilot platform and are not separately configurable by this application.

- **Human-in-the-loop by design:** The agent explicitly requires the engineer to review all findings before taking any action. The agent instruction file states: "Read-only, offline data only — all MCP tools query pre-collected diagnostic data; you do not connect to or interact with any production SQL Server. Never suggest applying configuration changes without explicit confirmation from the engineer." No automated remediation pathway exists in the system.

---

## 10. Best Practices for Deploying and Adopting the SQL Nexus Diagnostic Agent

Responsible AI is a shared commitment between Microsoft and its customers. While Microsoft builds AI applications and platform services with safety, fairness, and transparency at the core, customers play a critical role in deploying and using these technologies responsibly within their own contexts. To support this partnership, we offer the following best practices for deployers and end users.

Deployers and end-users should:

- **Exercise caution and evaluate outcomes when using the SQL Nexus Diagnostic Agent for consequential decisions or in sensitive domains:** Consequential decisions are those that may have a legal or significant impact on a person's access to education, employment, financial platforms, government benefits, healthcare, housing, insurance, legal platforms, or that could result in physical, psychological, or financial harm. Sensitive domains require particular care due to the potential for disproportionate impact on different groups of people. When using AI for decisions in these areas, make sure that impacted stakeholders can understand how decisions are made, appeal decisions, and update any relevant input data.

- **Evaluate legal and regulatory considerations:** Customers need to evaluate potential specific legal and regulatory obligations when using any AI platforms and solutions, which may not be appropriate for use in every industry or scenario. Additionally, AI platforms or solutions are not designed for and may not be used in ways prohibited in applicable terms of service and relevant codes of conduct.

End-users should:

- **Exercise human oversight when reviewing diagnostic conclusions:** While the agent produces data-grounded analysis, AI systems can make mistakes. Always verify that the agent's stated root cause is supported by the specific data values it cites — for example, confirming that a cited CPU percentage or wait count appears in the referenced tool output. Do not apply configuration changes or index modifications based solely on agent output without independent validation.

- **Be aware of the risk of overreliance:** Overreliance occurs when users accept incorrect or incomplete AI outputs, particularly because errors in diagnostic conclusions may be subtle and hard to detect without domain expertise. An incorrect diagnosis — for example, attributing performance degradation to blocking when the true cause is I/O — could lead an engineer to recommend the wrong fix to a customer, increasing case resolution time and reducing customer trust. Engineers should cross-reference agent conclusions with SQL Nexus reports or manual queries when the stakes are high.

- **Provide clear, specific symptom descriptions for best results:** The agent performs best when given a specific, focused symptom description rather than a vague request. For example, "The customer reports CPU is at 100% between 9am and 11am daily — is there a query driving it?" will yield more targeted results than "Check performance." Include any known time windows, application names, or database names that may help the agent narrow its analysis.

- **Report unexpected or incorrect outputs:** If the agent produces a conclusion that appears incorrect, does not match the data it cited, or behaves unexpectedly, report it through the SqlNexus GitHub repository at github.com/microsoft/SqlNexus by opening an issue. Include the symptom description, the tool outputs, and the agent conclusion so the team can investigate.

Deployers should:

- **Verify the SQL LogScout collection matches the symptom before starting a session:** The most common cause of unhelpful agent output is a mismatch between the reported symptom and the data that was collected. Before starting an agent session, confirm that the collection scenario used (for example, `GeneralPerf`, `DetailedPerf`, `Memory`) covers the tables required for the reported symptom. The skill file `AI/Skills/symptom-quick-reference.md` maps symptoms to recommended LogScout scenarios.

- **Keep the MCP server connection configuration local and secure:** The `mcp.json` file containing the SQL Server instance name and database name should remain gitignored and stored only on the engineer's local machine. Do not commit connection configuration to source control. Use Windows Integrated Authentication where possible to avoid storing credentials.

- **Monitor for schema drift between SQL Nexus versions:** The MCP tools assume specific table names and column structures from SQL Nexus. If a new version of SQL LogScout or PSSDiag introduces schema changes, some tools may return no data or partial data. After upgrading SQL LogScout, validate the key tools (`get_performance_summary`, `analyze_wait_stats`, `analyze_cpu_usage`) against a test collection before using the agent on production cases.

- **Scope additional testing to new collection scenarios:** The agent was validated against `GeneralPerf` and `DetailedPerf` collections. Engineers deploying the agent for less common scenarios (for example, Always On AG diagnostics, replication, or TempDB contention) should perform additional manual validation before relying on agent conclusions for those scenarios, as coverage in the current skill files is limited for these areas.

---

## 11. Learn More About the SQL Nexus Diagnostic Agent

For additional guidance or to learn more about the responsible use of the SQL Nexus Diagnostic Agent, we recommend reviewing the following documentation:

- [SqlNexus GitHub Repository](https://github.com/microsoft/SqlNexus) — source code, setup instructions, and issue tracking
- [SqlNexus MCP Server README](https://github.com/microsoft/SqlNexus/blob/MCPServer_SQLNexus_pijocoder_050126/SqlNexus.McpServer/README.md) — setup and configuration guide for the MCP server and agent
- [SQL LogScout](https://github.com/Microsoft/sql_logscout) — tool used to collect SQL Server diagnostic data
- [GitHub Copilot Responsible Use](https://docs.github.com/en/copilot/responsible-use-of-github-copilot-features/responsible-use-of-github-copilot-chat-in-your-ide) — GitHub Copilot's transparency documentation
- [Azure OpenAI Service Transparency Note](https://learn.microsoft.com/en-us/legal/cognitive-services/openai/transparency-note) — transparency information for GPT-4o and related models
- [Microsoft AI Principles](https://www.microsoft.com/en-us/ai/responsible-ai)
- [Microsoft Responsible AI Resources](https://www.microsoft.com/en-us/ai/responsible-ai-resources)
- [Microsoft Azure Learning Courses on Responsible AI](https://learn.microsoft.com/en-us/training/paths/responsible-ai-business-principles/)
