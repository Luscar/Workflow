import { Component, Input, OnChanges, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProcessDefinitionDto, ProcessNodeDto, NodeConnectionDto } from '../../models';

interface RenderedNode {
  id: string;
  name: string;
  type: string;
  x: number;
  y: number;
  width: number;
  height: number;
  isStart: boolean;
  color: string;
  icon: string;
  isActive?: boolean;
  isFailed?: boolean;
}

interface RenderedEdge {
  fromX: number;
  fromY: number;
  toX: number;
  toY: number;
  label?: string;
  path: string;
}

@Component({
  selector: 'app-flow-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './flow-viewer.html',
  styleUrl: './flow-viewer.scss'
})
export class FlowViewerComponent implements OnChanges, AfterViewInit {
  @Input() definition: ProcessDefinitionDto | null = null;
  @Input() activeNodeId: string | null = null;
  @Input() failedNodeId: string | null = null;
  @Input() executedNodeIds: string[] = [];

  @ViewChild('svgContainer') svgContainer!: ElementRef;

  renderedNodes: RenderedNode[] = [];
  renderedEdges: RenderedEdge[] = [];
  svgWidth = 800;
  svgHeight = 400;

  private readonly NODE_WIDTH = 180;
  private readonly NODE_HEIGHT = 50;
  private readonly H_SPACING = 60;
  private readonly V_SPACING = 80;

  private nodeColors: Record<string, string> = {
    'Business': '#3b82f6',
    'Decision': '#f59e0b',
    'Interactive': '#22c55e',
    'WaitUntilDate': '#06b6d4',
    'WaitForSignal': '#8b5cf6',
    'SubProcess': '#ec4899'
  };

  private nodeIcons: Record<string, string> = {
    'Business': 'B',
    'Decision': 'D',
    'Interactive': 'I',
    'WaitUntilDate': 'T',
    'WaitForSignal': 'S',
    'SubProcess': 'P'
  };

  ngAfterViewInit() {
    this.layoutGraph();
  }

  ngOnChanges() {
    this.layoutGraph();
  }

  layoutGraph() {
    if (!this.definition || !this.definition.nodes.length) return;

    const nodes = this.definition.nodes;
    const connections = this.definition.connections;

    // Build adjacency and compute levels using BFS
    const adjacency = new Map<string, string[]>();
    const inDegree = new Map<string, number>();

    nodes.forEach(n => {
      adjacency.set(n.id, []);
      inDegree.set(n.id, 0);
    });

    connections.forEach(c => {
      const list = adjacency.get(c.fromNodeId);
      if (list) list.push(c.toNodeId);
      inDegree.set(c.toNodeId, (inDegree.get(c.toNodeId) || 0) + 1);
    });

    // BFS from start node
    const levels = new Map<string, number>();
    const queue: string[] = [this.definition.startNodeId];
    levels.set(this.definition.startNodeId, 0);

    while (queue.length > 0) {
      const current = queue.shift()!;
      const currentLevel = levels.get(current)!;
      const neighbors = adjacency.get(current) || [];

      for (const neighbor of neighbors) {
        if (!levels.has(neighbor)) {
          levels.set(neighbor, currentLevel + 1);
          queue.push(neighbor);
        }
      }
    }

    // Assign levels to unvisited nodes
    nodes.forEach(n => {
      if (!levels.has(n.id)) levels.set(n.id, 0);
    });

    // Group nodes by level
    const levelGroups = new Map<number, ProcessNodeDto[]>();
    nodes.forEach(n => {
      const level = levels.get(n.id)!;
      if (!levelGroups.has(level)) levelGroups.set(level, []);
      levelGroups.get(level)!.push(n);
    });

    // Position nodes
    const maxLevel = Math.max(...Array.from(levelGroups.keys()));
    let maxNodesInLevel = 0;
    levelGroups.forEach(group => {
      if (group.length > maxNodesInLevel) maxNodesInLevel = group.length;
    });

    this.svgWidth = Math.max(800, (maxLevel + 1) * (this.NODE_WIDTH + this.H_SPACING) + 80);
    this.svgHeight = Math.max(300, maxNodesInLevel * (this.NODE_HEIGHT + this.V_SPACING) + 80);

    this.renderedNodes = [];
    const nodePositions = new Map<string, { x: number; y: number }>();

    for (let level = 0; level <= maxLevel; level++) {
      const group = levelGroups.get(level) || [];
      const totalHeight = group.length * this.NODE_HEIGHT + (group.length - 1) * this.V_SPACING;
      const startY = (this.svgHeight - totalHeight) / 2;

      group.forEach((node, idx) => {
        const x = 40 + level * (this.NODE_WIDTH + this.H_SPACING);
        const y = startY + idx * (this.NODE_HEIGHT + this.V_SPACING);

        nodePositions.set(node.id, { x, y });

        this.renderedNodes.push({
          id: node.id,
          name: node.name,
          type: node.type,
          x, y,
          width: this.NODE_WIDTH,
          height: this.NODE_HEIGHT,
          isStart: node.isStartNode,
          color: this.nodeColors[node.type] || '#6b7280',
          icon: this.nodeIcons[node.type] || '?',
          isActive: node.id === this.activeNodeId,
          isFailed: node.id === this.failedNodeId
        });
      });
    }

    // Build edges with curved paths
    this.renderedEdges = connections.map(c => {
      const from = nodePositions.get(c.fromNodeId);
      const to = nodePositions.get(c.toNodeId);
      if (!from || !to) return null;

      const fromX = from.x + this.NODE_WIDTH;
      const fromY = from.y + this.NODE_HEIGHT / 2;
      const toX = to.x;
      const toY = to.y + this.NODE_HEIGHT / 2;

      const midX = (fromX + toX) / 2;
      const path = `M ${fromX} ${fromY} C ${midX} ${fromY}, ${midX} ${toY}, ${toX} ${toY}`;

      return {
        fromX, fromY, toX, toY,
        label: c.label,
        path
      };
    }).filter(Boolean) as RenderedEdge[];
  }

  isExecuted(nodeId: string): boolean {
    return this.executedNodeIds.includes(nodeId);
  }

  getNodeClass(node: RenderedNode): string {
    let cls = 'flow-node';
    if (node.isActive) cls += ' node-active';
    if (node.isFailed) cls += ' node-failed';
    if (this.isExecuted(node.id)) cls += ' node-executed';
    return cls;
  }
}
