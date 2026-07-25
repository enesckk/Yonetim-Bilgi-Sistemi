import type { OrgNode } from '@/api/organizationApi'

export function flattenOrg(nodes: OrgNode[]): OrgNode[] {
  const out: OrgNode[] = []
  const walk = (list: OrgNode[]) => {
    for (const n of list) {
      out.push(n)
      if (n.children?.length) walk(n.children)
    }
  }
  walk(nodes)
  return out
}
