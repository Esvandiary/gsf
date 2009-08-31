package TVA.Hadoop.MapReduce.Historian;

import java.io.IOException;
import org.apache.hadoop.io.LongWritable;
import org.apache.hadoop.mapred.*;

import TVA.Hadoop.MapReduce.Historian.HistorianRecordReader;
import TVA.Hadoop.MapReduce.DatAware.File.StandardPointFile;

/**
 * Custom InputFormat class for reading DatAware .d files that store smartgrid PMU data
 * @author jpatter0
 *
 */
public class HistorianInputFormat extends FileInputFormat<LongWritable, StandardPointFile> implements JobConfigurable {
	
	public void configure(JobConf conf) {
	
	}

	public RecordReader<LongWritable, StandardPointFile> getRecordReader( InputSplit genericSplit, JobConf job, Reporter reporter ) throws IOException {
	    
		reporter.setStatus( genericSplit.toString() );
	
		return new HistorianRecordReader( job, (FileSplit)genericSplit );
	    
	}

}
