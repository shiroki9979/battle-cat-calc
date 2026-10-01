import type { SimulateResponse, StepDto } from '@/types/api';
import { Badge } from '@/components/ui/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

interface ResultPanelProps {
  result: SimulateResponse;
}

function StepRow({ step, index }: { step: StepDto; index: number }) {
  return (
    <TableRow>
      <TableCell className="font-mono text-muted-foreground">{index + 1}</TableCell>
      <TableCell className="font-mono">
        {step.source.id}{step.source.track}
      </TableCell>
      <TableCell>{step.unit}</TableCell>
      <TableCell className="text-muted-foreground text-xs">{step.poolId}</TableCell>
      <TableCell>
        {step.isGuaranteed && <Badge variant="secondary">G</Badge>}
      </TableCell>
    </TableRow>
  );
}

export function ResultPanel({ result }: ResultPanelProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          Result
          <Badge variant="outline">{result.totalCost} rolls</Badge>
        </CardTitle>
        <CardDescription>
          {result.singleRollCount} single + {result.guaranteedCount} guaranteed
          {result.endPos &&
            ` — ends at ${result.endPos.id}${result.endPos.track}`}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Collected cats */}
        <div className="flex flex-wrap gap-1.5">
          {result.collected.map((name) => (
            <Badge key={name}>{name}</Badge>
          ))}
        </div>

        {/* Path table */}
        {result.path.length > 0 && (
          <div className="rounded-lg border overflow-auto max-h-[50vh]">
            <Table>
              <TableHeader className="sticky top-0 bg-background">
                <TableRow>
                  <TableHead className="w-12">#</TableHead>
                  <TableHead className="w-16">From</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Pool</TableHead>
                  <TableHead className="w-12">Type</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {result.path.map((step, i) => (
                  <StepRow
                    key={`${step.source.id}${step.source.track}-${i}`}
                    step={step}
                    index={i}
                  />
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
