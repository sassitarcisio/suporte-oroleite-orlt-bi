import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import LoginScreen from './LoginScreen'

describe('Corporate login', () => {
  it('renders accessible fields, mobile identity and no public registration', () => {
    render(<LoginScreen initialEmail="" loading={false} errorMessage="" onSubmit={vi.fn()} />)
    expect(screen.getByRole('heading', { name: 'Acesse o BI Oroleite' })).toBeVisible()
    expect(screen.getByText('ACESSO RESTRITO')).toBeVisible()
    expect(screen.getByLabelText('E-MAIL')).toHaveAttribute('autocomplete', 'username')
    expect(screen.getByLabelText('SENHA')).toHaveAttribute('autocomplete', 'current-password')
    expect(screen.queryByRole('button', { name: 'Criar minha conta' })).not.toBeInTheDocument()
    expect(document.querySelector('.login-mobile-identity')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'ENTRAR' })).toHaveClass('login-submit')
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }))
    expect(screen.getByLabelText('SENHA')).toHaveAttribute('type', 'text')
    fireEvent.click(screen.getByRole('button', { name: 'Ocultar senha' }))
    expect(screen.getByLabelText('SENHA')).toHaveAttribute('type', 'password')
  })
  it('sends rememberDevice and blocks repeated submissions while awaiting login', async () => {
    let finish: () => void = () => {}
    const submit = vi.fn(() => new Promise<void>(resolve => { finish = resolve }))
    render(<LoginScreen initialEmail="ana@example.com" loading={false} errorMessage="" onSubmit={submit} />)
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'ExamplePassword123!' } })
    fireEvent.click(screen.getByLabelText('Manter-me conectado neste dispositivo'))
    const form = screen.getByRole('button', { name: 'ENTRAR' }).closest('form')!
    fireEvent.submit(form)
    fireEvent.submit(form)
    expect(submit).toHaveBeenCalledExactlyOnceWith('ana@example.com', 'ExamplePassword123!', true)
    expect(screen.getByRole('button', { name: 'ENTRANDO...' })).toBeDisabled()
    finish()
    await waitFor(() => expect(screen.getByRole('button', { name: 'ENTRAR' })).toBeEnabled())
  })
  it('opens keyboard accessible password help and restores focus', () => {
    render(<LoginScreen initialEmail="" loading={false} errorMessage="Usuário ou senha incorretos." onSubmit={vi.fn()} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Usuário ou senha incorretos.')
    const help = screen.getByRole('button', { name: 'Esqueci minha senha' })
    fireEvent.click(help)
    expect(screen.getByRole('dialog')).toHaveTextContent('Solicite a redefinição de senha ao administrador do BI Oroleite.')
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(help).toHaveFocus()
  })
})
