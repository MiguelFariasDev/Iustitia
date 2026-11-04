import type { ColumnDef } from '@tanstack/react-table'
import { screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { DataTable } from './DataTable'

interface Row {
  id: string
  name: string
  age: number
}

const rows: Row[] = Array.from({ length: 12 }, (_, i) => ({
  id: String(i),
  name: `Pessoa ${String(i + 1).padStart(2, '0')}`,
  age: 20 + i,
}))
const columns: ColumnDef<Row, unknown>[] = [
  { accessorKey: 'name', header: 'Nome' },
  { accessorKey: 'age', header: 'Idade' },
]

describe('DataTable', () => {
  it('pagina os dados e navega entre páginas', async () => {
    const { user } = renderWithProviders(
      <DataTable caption="Pessoas" columns={columns} data={rows} pageSize={5} />
    )
    expect(screen.getAllByRole('row')).toHaveLength(6) // cabeçalho + 5
    expect(screen.getByText(/página/i)).toHaveTextContent('Página 1 de 3')
    await user.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(screen.getByText('Pessoa 06')).toBeInTheDocument()
  })

  it('ordena ao clicar no cabeçalho e expõe aria-sort', async () => {
    const { user } = renderWithProviders(
      <DataTable caption="Pessoas" columns={columns} data={rows} pageSize={20} />
    )
    await user.click(screen.getByRole('button', { name: /idade/i }))
    await user.click(screen.getByRole('button', { name: /idade/i }))
    expect(screen.getByRole('columnheader', { name: /idade/i })).toHaveAttribute(
      'aria-sort',
      'descending'
    )
    const firstRow = screen.getAllByRole('row')[1]
    expect(within(firstRow as HTMLElement).getByText('31')).toBeInTheDocument()
  })

  it('filtra pela busca e mostra estado vazio', async () => {
    const { user } = renderWithProviders(
      <DataTable caption="Pessoas" columns={columns} data={rows} />
    )
    await user.type(screen.getByRole('searchbox'), 'inexistente')
    expect(await screen.findByText('Nenhum registro encontrado')).toBeInTheDocument()
  })

  describe('seleção múltipla', () => {
    it('marca linhas, informa o novo conjunto e usa o rótulo acessível de cada linha', async () => {
      const onChange = vi.fn()
      const { user } = renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={rows.slice(0, 3)}
          enableSearch={false}
          getRowId={(row) => row.id}
          selection={{
            selected: new Set(['1']),
            onChange,
            rowLabel: (row) => `Selecionar ${row.name}`,
          }}
        />
      )

      expect(screen.getByRole('checkbox', { name: 'Selecionar Pessoa 02' })).toBeChecked()
      await user.click(screen.getByRole('checkbox', { name: 'Selecionar Pessoa 01' }))

      expect(onChange).toHaveBeenCalledWith(new Set(['1', '0']))
    })

    it('o cabeçalho seleciona só as linhas DA PÁGINA e fica indeterminado com seleção parcial', async () => {
      const onChange = vi.fn()
      const { user } = renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={rows.slice(0, 3)}
          enableSearch={false}
          getRowId={(row) => row.id}
          selection={{ selected: new Set(['0', '99']), onChange, rowLabel: (r) => r.name }}
        />
      )

      const header = screen.getByRole('checkbox', {
        name: 'Selecionar todas as linhas desta página',
      })
      expect(header).toHaveAttribute('aria-checked', 'mixed')

      await user.click(header)

      // Completa a página e preserva o que já estava marcado fora dela.
      expect(onChange).toHaveBeenCalledWith(new Set(['0', '99', '1', '2']))
    })

    it('com tudo marcado, o cabeçalho desmarca apenas as linhas da página', async () => {
      const onChange = vi.fn()
      const { user } = renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={rows.slice(0, 2)}
          enableSearch={false}
          getRowId={(row) => row.id}
          selection={{ selected: new Set(['0', '1', '99']), onChange, rowLabel: (r) => r.name }}
        />
      )

      await user.click(
        screen.getByRole('checkbox', { name: 'Selecionar todas as linhas desta página' })
      )

      expect(onChange).toHaveBeenCalledWith(new Set(['99']))
    })
  })

  describe('paginação e ordenação no servidor', () => {
    it('mostra a página do servidor sem repaginar localmente', async () => {
      const onPageChange = vi.fn()
      const { user } = renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={rows.slice(0, 3)}
          enableSearch={false}
          serverPagination={{ page: 2, pageCount: 4, totalCount: 80, onPageChange }}
        />
      )

      // Só as 3 linhas recebidas, mesmo com pageSize padrão de 10.
      expect(screen.getAllByRole('row')).toHaveLength(4)
      expect(screen.getByText(/página/i)).toHaveTextContent('Página 2 de 4')
      expect(screen.getByText(/80/)).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: 'Próxima página' }))
      expect(onPageChange).toHaveBeenCalledWith(3)
    })

    it('emite o estado de ordenação em vez de reordenar as linhas', async () => {
      const onChange = vi.fn()
      const { user } = renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={rows.slice(0, 3)}
          enableSearch={false}
          serverSorting={{ value: [{ id: 'age', desc: true }], onChange }}
        />
      )

      expect(screen.getByRole('columnheader', { name: /idade/i })).toHaveAttribute(
        'aria-sort',
        'descending'
      )
      // Ordem recebida preservada.
      expect(screen.getAllByRole('row')[1]).toHaveTextContent('Pessoa 01')

      await user.click(screen.getByRole('button', { name: /nome/i }))

      expect(onChange).toHaveBeenCalledWith([{ id: 'name', desc: false }])
    })

    it('usa o estado vazio próprio quando informado', () => {
      renderWithProviders(
        <DataTable
          caption="Pessoas"
          columns={columns}
          data={[]}
          enableSearch={false}
          emptyNode={<p>Nada casou com o filtro</p>}
        />
      )

      expect(screen.getByText('Nada casou com o filtro')).toBeInTheDocument()
    })
  })
})
