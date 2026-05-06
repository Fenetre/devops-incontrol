/**
 * CSV export utility composable.
 * Usage: const { exportCsv } = useCsvExport()
 *        exportCsv(rows, columns, filename)
 */
export function useCsvExport() {
  function escapeCsv(value) {
    if (value == null) return ''
    const str = String(value)
    if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r')) {
      return '"' + str.replace(/"/g, '""') + '"'
    }
    return str
  }

  /**
   * @param {Array<Object>} rows - Data rows
   * @param {Array<{key: string, label: string, format?: (row) => string}>} columns - Column definitions
   * @param {string} filename - Output filename (without .csv)
   */
  function exportCsv(rows, columns, filename = 'export') {
    if (!rows || rows.length === 0) return

    const header = columns.map(c => escapeCsv(c.label)).join(',')
    const body = rows.map(row =>
      columns.map(col => {
        const val = col.format ? col.format(row) : row[col.key]
        return escapeCsv(val)
      }).join(',')
    ).join('\r\n')

    const bom = '\uFEFF'
    const csv = bom + header + '\r\n' + body
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${filename}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return { exportCsv }
}
