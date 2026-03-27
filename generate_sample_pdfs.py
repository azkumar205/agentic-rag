"""Generate sample business PDFs for Agentic RAG testing."""
import os
from fpdf import FPDF

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "documents")
os.makedirs(OUTPUT_DIR, exist_ok=True)


def create_contract_pdf():
    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=25)

    # Page 1 — Cover / Header
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 22)
    pdf.cell(0, 15, "Master Services Agreement", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.set_font("Helvetica", "", 12)
    pdf.cell(0, 10, "Contract Number: CTR-2024-001", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.cell(0, 10, "Effective Date: January 1, 2024", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.ln(10)

    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "This Master Services Agreement (the \"Agreement\") is entered into as of January 1, 2024, "
        "by and between Contoso Ltd (\"Provider\"), a corporation organized under the laws of the "
        "State of Washington, with its principal place of business at 1 Microsoft Way, Redmond, WA 98052, "
        "and Fabrikam Inc (\"Client\"), a corporation organized under the laws of the State of California, "
        "with its principal place of business at 500 Innovation Drive, San Jose, CA 95134."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "1. Scope of Services", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "Provider shall deliver the following services to the Client:\n\n"
        "  a) Custom application development using .NET 8 and Azure cloud services.\n"
        "  b) Architecture design and review for cloud-native microservices.\n"
        "  c) Implementation of AI-powered search and retrieval-augmented generation (RAG) solutions.\n"
        "  d) Ongoing maintenance and support for a period of twelve (12) months.\n\n"
        "The total contract value is USD 120,000.00, payable in monthly installments of USD 10,000.00."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "2. Payment Terms", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "2.1 Invoices shall be issued on the 15th of each month for services rendered during "
        "the preceding calendar month.\n\n"
        "2.2 Payment is due within thirty (30) days of the invoice date.\n\n"
        "2.3 Late payments shall accrue interest at a rate of 1.5% per month on the outstanding balance.\n\n"
        "2.4 All payments shall be made in United States Dollars (USD) via wire transfer to the "
        "account designated by Provider."
    ))

    # Page 2 — Terms continued
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "3. Intellectual Property", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "3.1 All intellectual property developed under this Agreement shall be owned by the Client "
        "upon full payment of all invoices.\n\n"
        "3.2 Provider retains a non-exclusive license to use general-purpose tools, frameworks, "
        "and methodologies developed during the engagement for other client projects.\n\n"
        "3.3 Pre-existing intellectual property of either party remains the property of that party."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "4. Confidentiality", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "4.1 Both parties agree to maintain the confidentiality of all proprietary information "
        "exchanged during the term of this Agreement.\n\n"
        "4.2 Confidential information includes, but is not limited to: source code, business plans, "
        "customer data, financial information, and technical specifications.\n\n"
        "4.3 The confidentiality obligations shall survive for a period of three (3) years following "
        "the termination of this Agreement."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "5. Service Level Agreements (SLA)", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "5.1 Provider guarantees 99.9% uptime for all production systems.\n\n"
        "5.2 Critical issues (Severity 1) shall be responded to within 1 hour and resolved within 4 hours.\n\n"
        "5.3 Major issues (Severity 2) shall be responded to within 4 hours and resolved within 24 hours.\n\n"
        "5.4 Minor issues (Severity 3) shall be responded to within 1 business day and resolved within 5 business days.\n\n"
        "5.5 Monthly SLA reports shall be provided to the Client by the 5th business day of each month."
    ))

    # Page 3 — Termination & Signatures
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "6. Termination", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "6.1 Either party may terminate this Agreement with sixty (60) days written notice.\n\n"
        "6.2 In the event of termination, Client shall pay for all services rendered up to the "
        "effective date of termination.\n\n"
        "6.3 Provider shall deliver all work products, documentation, and source code within "
        "fifteen (15) business days of the termination date.\n\n"
        "6.4 Upon termination, both parties shall return or destroy all confidential information "
        "of the other party within thirty (30) days."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "7. Limitation of Liability", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "7.1 Neither party shall be liable for any indirect, incidental, special, or consequential damages.\n\n"
        "7.2 The total liability of Provider under this Agreement shall not exceed the total "
        "contract value of USD 120,000.00.\n\n"
        "7.3 This limitation of liability shall not apply to breaches of confidentiality or "
        "intellectual property provisions."
    ))
    pdf.ln(15)
    pdf.set_font("Helvetica", "B", 12)
    pdf.cell(0, 10, "SIGNATURES", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.ln(10)
    pdf.set_font("Helvetica", "", 11)
    pdf.cell(90, 7, "______________________________")
    pdf.cell(90, 7, "______________________________", new_x="LMARGIN", new_y="NEXT")
    pdf.cell(90, 7, "John Smith, CEO")
    pdf.cell(90, 7, "Jane Doe, CTO", new_x="LMARGIN", new_y="NEXT")
    pdf.cell(90, 7, "Contoso Ltd (Provider)")
    pdf.cell(90, 7, "Fabrikam Inc (Client)", new_x="LMARGIN", new_y="NEXT")
    pdf.cell(90, 7, "Date: January 1, 2024")
    pdf.cell(90, 7, "Date: January 1, 2024", new_x="LMARGIN", new_y="NEXT")

    path = os.path.join(OUTPUT_DIR, "sample-contract.pdf")
    pdf.output(path)
    print(f"  Created: {path} ({os.path.getsize(path):,} bytes, {pdf.pages_count} pages)")


def create_policy_pdf():
    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=25)

    # Page 1
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 22)
    pdf.cell(0, 15, "Cloud Infrastructure Policy", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.set_font("Helvetica", "", 12)
    pdf.cell(0, 10, "Fabrikam Inc  |  Document ID: POL-CLOUD-2024", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.cell(0, 10, "Last Updated: March 15, 2024  |  Version 2.1", new_x="LMARGIN", new_y="NEXT", align="C")
    pdf.ln(10)

    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "1. Purpose and Scope", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "This policy establishes the standards and guidelines for the use of cloud infrastructure "
        "services at Fabrikam Inc. It applies to all employees, contractors, and third-party vendors "
        "who access or manage cloud resources on behalf of the organization.\n\n"
        "The policy covers Microsoft Azure as the primary cloud platform, with supplementary guidance "
        "for multi-cloud scenarios involving AWS and Google Cloud Platform."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "2. Resource Provisioning Standards", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "2.1 All cloud resources must be provisioned using Infrastructure as Code (IaC). "
        "Bicep is the approved IaC language for Azure deployments.\n\n"
        "2.2 Resources must be tagged with the following mandatory tags:\n"
        "  - Environment (dev, staging, production)\n"
        "  - CostCenter (department cost code)\n"
        "  - Owner (team or individual email)\n"
        "  - Project (project identifier)\n\n"
        "2.3 Production resources must be deployed in paired Azure regions for disaster recovery. "
        "The primary region is East US 2 and the secondary region is West US 2.\n\n"
        "2.4 All resource names must follow the naming convention: {resource-type}-{project}-{env}-{region}."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "3. Security Requirements", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "3.1 All data at rest must be encrypted using AES-256 encryption.\n\n"
        "3.2 All data in transit must use TLS 1.2 or higher.\n\n"
        "3.3 Azure Key Vault must be used for all secrets, certificates, and encryption keys.\n\n"
        "3.4 Managed identities must be used instead of service principals wherever possible.\n\n"
        "3.5 Network security groups (NSGs) must be applied to all subnets. Default deny rules must be in place."
    ))

    # Page 2
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "4. Cost Management", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "4.1 Monthly cloud spending must not exceed the approved budget without VP-level approval.\n\n"
        "4.2 Azure Cost Management alerts must be configured at 50%, 75%, and 90% of budget thresholds.\n\n"
        "4.3 Development and test environments must use auto-shutdown schedules (6 PM to 8 AM local time, "
        "weekends off) to reduce costs.\n\n"
        "4.4 Reserved Instances must be purchased for stable production workloads with 1-year or 3-year terms.\n\n"
        "4.5 Monthly cost reviews are mandatory for all teams with cloud spending exceeding USD 5,000/month."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "5. AI and Machine Learning Services", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "5.1 Azure OpenAI Service is the approved platform for generative AI workloads.\n\n"
        "5.2 All AI models must be deployed with content filtering enabled (default settings).\n\n"
        "5.3 Prompt injection protection must be implemented for all customer-facing AI applications.\n\n"
        "5.4 AI Search (formerly Cognitive Search) must be used for enterprise search and RAG patterns.\n\n"
        "5.5 Embedding models must use text-embedding-3-large with 1536 dimensions for consistency.\n\n"
        "5.6 GPT-4o is the approved model for production chat and completion workloads. "
        "GPT-4o-mini may be used for development and testing.\n\n"
        "5.7 All AI usage must comply with the Microsoft Responsible AI principles."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "6. Data Residency and Compliance", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "6.1 Customer data classified as 'Confidential' or higher must remain in US-based Azure regions.\n\n"
        "6.2 Azure OpenAI resources must be deployed in regions that support data processing boundaries "
        "(East US 2, West US, or Sweden Central).\n\n"
        "6.3 All cloud services must comply with SOC 2 Type II and ISO 27001 standards.\n\n"
        "6.4 Annual third-party audits are required for all production cloud environments."
    ))

    # Page 3
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "7. Monitoring and Incident Response", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "7.1 Application Insights must be enabled for all web applications and APIs.\n\n"
        "7.2 Log Analytics workspace must retain logs for a minimum of 90 days.\n\n"
        "7.3 Azure Monitor alerts must be configured for:\n"
        "  - CPU utilization > 80% for 5 consecutive minutes\n"
        "  - Memory utilization > 85% for 5 consecutive minutes\n"
        "  - HTTP 5xx error rate > 1% of total requests\n"
        "  - Response time P95 > 3 seconds\n\n"
        "7.4 Incident response team must be notified within 15 minutes of a Severity 1 alert.\n\n"
        "7.5 Post-incident reviews must be completed within 5 business days of incident resolution."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "8. Backup and Disaster Recovery", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "8.1 All production databases must have automated backups with point-in-time restore capability.\n\n"
        "8.2 Recovery Point Objective (RPO): Maximum 1 hour for Tier 1 applications.\n\n"
        "8.3 Recovery Time Objective (RTO): Maximum 4 hours for Tier 1 applications.\n\n"
        "8.4 Disaster recovery drills must be conducted quarterly.\n\n"
        "8.5 Geo-redundant storage (GRS) must be used for all critical data stores."
    ))
    pdf.ln(5)
    pdf.set_font("Helvetica", "B", 14)
    pdf.cell(0, 10, "9. Policy Enforcement", new_x="LMARGIN", new_y="NEXT")
    pdf.set_font("Helvetica", "", 11)
    pdf.multi_cell(0, 7, (
        "9.1 Azure Policy will be used to enforce compliance with this document.\n\n"
        "9.2 Non-compliant resources will be flagged in the monthly compliance dashboard.\n\n"
        "9.3 Teams with persistent non-compliance (3+ consecutive months) will have their "
        "provisioning privileges reviewed.\n\n"
        "9.4 This policy will be reviewed and updated annually, or as needed in response to "
        "significant changes in technology or business requirements."
    ))
    pdf.ln(10)
    pdf.set_font("Helvetica", "I", 10)
    pdf.cell(0, 7, "Approved by: Cloud Governance Board  |  Next Review: March 2025", align="C")

    path = os.path.join(OUTPUT_DIR, "cloud-infrastructure-policy.pdf")
    pdf.output(path)
    print(f"  Created: {path} ({os.path.getsize(path):,} bytes, {pdf.pages_count} pages)")


if __name__ == "__main__":
    print("Generating sample PDFs...")
    create_contract_pdf()
    create_policy_pdf()
    print("\nDone! PDFs ready in ./documents/")
