/**
 * Module codes that use Steelstone ERP-FORMATS PDF (row Print / Print PDF).
 * 08 Vendor bill | 09 Quotation | 10 Payment/expense voucher | 11 Sales tax invoice
 * 14 Delivery challan | 15 Payment receipt | 16 Purchase order
 * 17 Customer account statement | 18 Factory ledger
 */
export const ERP_DOCUMENT_MODULE_CODES = new Set([
  '08',
  '09',
  '10',
  '11',
  '14',
  '15',
  '16',
  '17',
  '18',
])

export function moduleHasErpPdfPrint(moduleCode: string): boolean {
  return ERP_DOCUMENT_MODULE_CODES.has(moduleCode.trim())
}
