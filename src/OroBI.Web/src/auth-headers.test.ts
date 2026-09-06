import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('Published authentication page content restrictions', () => {
  it('allows only bundled scripts and the official API while preventing framing', () => {
    const config = JSON.parse(readFileSync('public/staticwebapp.config.json', 'utf8'))
    const csp = config.globalHeaders?.['Content-Security-Policy'] ?? ''
    const directive = (name: string) => csp.split(';').map((part: string) => part.trim()).find((part: string) => part.startsWith(`${name} `)) ?? ''
    expect(directive('script-src')).toBe("script-src 'self'")
    expect(directive('connect-src')).toContain('https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io')
    expect(directive('connect-src')).not.toContain('*')
    expect(directive('frame-ancestors')).toBe("frame-ancestors 'none'")
    expect(directive('object-src')).toBe("object-src 'none'")
  })
})
